using System.Collections.Concurrent;
using LibreMetaverse;
using Microsoft.Extensions.Logging;
using SecondSight.Core.Perception;

namespace SecondSight.Core.Session;

/// <summary>Perception: what the bot can see around it.</summary>
public sealed partial class BotSession
{
    private static readonly TimeSpan PropertiesWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly ConcurrentDictionary<UUID, Primitive.ObjectProperties> _objectProperties = new();
    private readonly ConcurrentDictionary<UUID, string> _names = new();
    private volatile CoarsePositions? _coarse;

    private sealed record CoarsePositions(ulong RegionHandle, IReadOnlyDictionary<UUID, Vector3> Positions);

    private void AttachPerception(GridClient client)
    {
        client.Objects.ObjectPropertiesFamily += OnObjectPropertiesFamily;
        client.Objects.ObjectProperties += OnObjectProperties;
        client.Grid.CoarseLocationUpdate += OnCoarseLocationUpdate;
        client.Avatars.UUIDNameReply += OnUuidNameReply;
    }

    private void DetachPerception(GridClient client)
    {
        client.Objects.ObjectPropertiesFamily -= OnObjectPropertiesFamily;
        client.Objects.ObjectProperties -= OnObjectProperties;
        client.Grid.CoarseLocationUpdate -= OnCoarseLocationUpdate;
        client.Avatars.UUIDNameReply -= OnUuidNameReply;
    }

    private void OnObjectPropertiesFamily(object? sender, ObjectPropertiesFamilyEventArgs e) => StoreProperties(e.Properties);

    private void OnObjectProperties(object? sender, ObjectPropertiesEventArgs e) => StoreProperties(e.Properties);

    private void StoreProperties(Primitive.ObjectProperties? props)
    {
        if (props == null || props.ObjectID == UUID.Zero) return;
        // The family reply has no sit or touch text; keep them from a fuller reply if there was one.
        if (_objectProperties.TryGetValue(props.ObjectID, out var old) && string.IsNullOrEmpty(props.SitName))
        {
            props.SitName = old.SitName;
            props.TouchName = old.TouchName;
        }
        _objectProperties[props.ObjectID] = props;
    }

    private void OnCoarseLocationUpdate(object? sender, CoarseLocationUpdateEventArgs e)
    {
        if (e.Simulator == null || e.Positions == null) return;
        if (_client?.Network.CurrentSim is { } current && current.Handle != e.Simulator.Handle) return;
        _coarse = new CoarsePositions(e.Simulator.Handle, new Dictionary<UUID, Vector3>(e.Positions));
    }

    private void OnUuidNameReply(object? sender, UUIDNameReplyEventArgs e)
    {
        foreach (var (id, name) in e.Names)
            if (!string.IsNullOrWhiteSpace(name)) _names[id] = name;
    }

    private string? NameOf(UUID id) => _names.TryGetValue(id, out var n) ? n : null;

    public async Task<SessionResult> DescribeSurroundingsAsync(float radius, int maxObjects, CancellationToken cancellationToken)
    {
        if (!TryGetClient(out var client, out var notLoggedIn)) return notLoggedIn;
        var sim = client.Network.CurrentSim;
        if (sim == null) return new(false, "The bot isn't in a region right now (it may be teleporting). Try again in a moment.");

        var me = client.Self.SimPosition;
        var objects = NearbyObjects(sim, me, radius);
        var avatars = AvatarsInRegion(client, sim);

        // Names and descriptions aren't sent with objects: ask for the ones we'll show, and for unknown avatar names.
        var sittingOnRoot = client.Self.SittingOn != 0 ? RootOf(sim, client.Self.SittingOn) : null;
        var wantProps = objects.Take(maxObjects).Where(o => o.Name == null && o.Kind == SceneObjectKind.Object).Select(o => o.Id).ToList();
        if (sittingOnRoot != null && !_objectProperties.ContainsKey(sittingOnRoot.ID)) wantProps.Add(sittingOnRoot.ID);
        var wantNames = avatars.Where(a => a.Name == null).Select(a => a.Id).ToList();
        var parcelTask = CurrentParcelAsync(client, sim, me, cancellationToken);
        await FetchAsync(client, sim, wantProps, wantNames, cancellationToken);

        if (wantProps.Count > 0) objects = NearbyObjects(sim, me, radius);
        if (wantNames.Count > 0) avatars = AvatarsInRegion(client, sim);
        var parcel = await parcelTask;

        var owner = _owner.Id is { } ownerId ? avatars.FirstOrDefault(a => a.Id == ownerId) : null;
        var scene = new Scene
        {
            Region = sim.Name,
            ParcelName = SceneBuilder.Clean(parcel?.Name),
            ParcelDescription = SceneBuilder.Clean(parcel?.Desc),
            SelfPosition = me,
            SittingOn = sittingOnRoot == null ? null : ObjectName(sittingOnRoot),
            Flying = client.Self.Movement.Fly,
            OwnerName = OwnerName,
            Owner = owner,
            Avatars = avatars,
            Objects = objects.Take(maxObjects).ToList(),
            ObjectsInRange = objects.Count,
            Radius = radius,
        };
        return new(true, SceneFormatter.Describe(scene, maxObjects));
    }

    public async Task<SessionResult> InspectAsync(string id, CancellationToken cancellationToken)
    {
        if (!TryGetClient(out var client, out var notLoggedIn)) return notLoggedIn;
        var sim = client.Network.CurrentSim;
        if (sim == null) return new(false, "The bot isn't in a region right now (it may be teleporting). Try again in a moment.");
        var me = client.Self.SimPosition;
        id = id.Trim();

        Primitive? prim = null;
        UUID uuid = UUID.Zero;
        if (UUID.TryParse(id, out uuid))
        {
            var avatar = AvatarsInRegion(client, sim).FirstOrDefault(a => a.Id == uuid);
            if (avatar != null)
            {
                if (avatar.Name == null) await FetchAsync(client, sim, [], [uuid], cancellationToken);
                avatar = avatar with { Name = avatar.Name ?? NameOf(uuid) };
                string? seat = null;
                if (sim.ObjectsAvatars.Values.FirstOrDefault(a => a.ID == uuid) is { ParentID: not 0 } av && RootOf(sim, av.ParentID) is { } seatRoot)
                    seat = ObjectName(seatRoot);
                return new(true, ObjectDetailsFormatter.DescribeAvatar(avatar, me, uuid == _owner.Id, seat));
            }
            prim = sim.ObjectsPrimitives.Values.FirstOrDefault(p => p.ID == uuid);
        }
        else if (uint.TryParse(id, out var localId))
        {
            sim.ObjectsPrimitives.TryGetValue(localId, out prim);
        }
        else
        {
            return new(false, $"\"{id}\" isn't an object id. Use the id from describe_surroundings (a UUID like 1234abcd-...).");
        }

        if (prim == null)
            return new(false, "That object isn't in view. The bot only sees objects in its own region within about 128 m. " +
                "Run describe_surroundings for current ids.");

        var root = RootOf(sim, prim.LocalID) ?? prim;
        Avatar? wearer = null;
        if (root.ParentID != 0 && sim.ObjectsAvatars.TryGetValue(root.ParentID, out var w)) wearer = w;

        // Ask again even if cached, so the details are fresh, and also for the whole object when this is one part.
        _objectProperties.TryRemove(prim.ID, out _);
        var wantProps = new List<UUID> { prim.ID };
        if (root.ID != prim.ID && !_objectProperties.ContainsKey(root.ID)) wantProps.Add(root.ID);
        await FetchAsync(client, sim, wantProps, [], cancellationToken);

        _objectProperties.TryGetValue(prim.ID, out var props);
        var names = new List<UUID>();
        if (props != null)
        {
            if (props.OwnerID != UUID.Zero && props.OwnerID != props.GroupID && NameOf(props.OwnerID) == null) names.Add(props.OwnerID);
            if (props.CreatorID != UUID.Zero && NameOf(props.CreatorID) == null) names.Add(props.CreatorID);
        }
        if (wearer != null && string.IsNullOrWhiteSpace(wearer.Name) && NameOf(wearer.ID) == null) names.Add(wearer.ID);
        if (names.Count > 0) await FetchAsync(client, sim, [], names, cancellationToken);

        var childCount = sim.ObjectsPrimitives.Values.Count(p => p.ParentID == prim.LocalID);
        var position = Geometry.WorldPosition(prim, localId => sim.ObjectsPrimitives.TryGetValue(localId, out var p) ? p : null);
        var seen = SceneBuilder.ToSeen(prim, SceneBuilder.KindOf(prim) ?? SceneObjectKind.Object, childCount + 1,
            hasSitter: sim.ObjectsAvatars.Values.Any(a => a.ParentID == prim.LocalID), props) with { Position = position };

        var details = new ObjectDetailsFormatter.Details
        {
            Object = seen,
            Properties = props,
            OwnerName = props == null ? null : StripResident(NameOf(props.OwnerID)),
            CreatorName = props == null ? null : StripResident(NameOf(props.CreatorID)),
            PartOf = root.ID != prim.ID && wearer == null ? ObjectName(root) : null,
            WornBy = wearer == null ? null : StripResident(string.IsNullOrWhiteSpace(wearer.Name) ? NameOf(wearer.ID) : wearer.Name) ?? "an avatar",
            SitText = SceneBuilder.Clean(props?.SitName),
            Physical = (prim.Flags & PrimFlags.Physics) != 0,
            Phantom = (prim.Flags & PrimFlags.Phantom) != 0,
            Temporary = (prim.Flags & (PrimFlags.Temporary | PrimFlags.TemporaryOnRez)) != 0,
        };
        return new(true, ObjectDetailsFormatter.Describe(details, me));
    }

    private List<SeenObject> NearbyObjects(Simulator sim, Vector3 me, float radius)
    {
        var prims = sim.ObjectsPrimitives.Values.ToList();
        var seated = new HashSet<uint>();
        foreach (var a in sim.ObjectsAvatars.Values)
            if (a.ParentID != 0 && RootOf(sim, a.ParentID) is { } seat) seated.Add(seat.LocalID);
        return SceneBuilder.NearbyObjects(prims, me, radius, seated,
            id => _objectProperties.TryGetValue(id, out var p) ? p : null);
    }

    private List<SeenAvatar> AvatarsInRegion(GridClient client, Simulator sim)
    {
        var coarse = _coarse is { } c && c.RegionHandle == sim.Handle ? c.Positions : null;
        return SceneBuilder.Avatars(sim.ObjectsAvatars.Values.ToList(), coarse, client.Self.AgentID,
            localId => sim.ObjectsPrimitives.TryGetValue(localId, out var p) ? p : null, NameOf);
    }

    /// <summary>The root prim of the object that <paramref name="localId"/> belongs to, if it is in view.</summary>
    private static Primitive? RootOf(Simulator sim, uint localId)
    {
        if (!sim.ObjectsPrimitives.TryGetValue(localId, out var prim)) return null;
        for (var depth = 0; prim.ParentID != 0 && depth < 4; depth++)
        {
            if (!sim.ObjectsPrimitives.TryGetValue(prim.ParentID, out var parent)) break;
            prim = parent;
        }
        return prim;
    }

    private string ObjectName(Primitive prim) =>
        SceneBuilder.Clean(_objectProperties.TryGetValue(prim.ID, out var p) ? p.Name : prim.Properties?.Name) is { } name
            ? $"\"{name}\""
            : "an unnamed object";

    private static string? StripResident(string? name) =>
        name?.EndsWith(" Resident", StringComparison.OrdinalIgnoreCase) == true ? name[..^" Resident".Length] : name;

    /// <summary>Asks the region for object details and the grid for avatar names, then waits a few seconds for answers.</summary>
    private async Task FetchAsync(GridClient client, Simulator sim, IReadOnlyCollection<UUID> objectIds,
        IReadOnlyCollection<UUID> avatarIds, CancellationToken cancellationToken)
    {
        var objects = objectIds.Where(id => !_objectProperties.ContainsKey(id)).Distinct().ToList();
        var avatars = avatarIds.Where(id => NameOf(id) == null).Distinct().ToList();
        if (objects.Count == 0 && avatars.Count == 0) return;

        try
        {
            foreach (var id in objects) client.Objects.RequestObjectPropertiesFamily(sim, id);
            if (avatars.Count > 0) client.Avatars.RequestAvatarNames(avatars);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Requesting object details failed");
            return;
        }

        var deadline = _time.GetUtcNow() + PropertiesWait;
        while (_time.GetUtcNow() < deadline)
        {
            if (objects.All(_objectProperties.ContainsKey) && avatars.All(id => NameOf(id) != null)) return;
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    /// <summary>The parcel the bot stands on, asking the region for it when it isn't known yet.</summary>
    private async Task<Parcel?> CurrentParcelAsync(GridClient client, Simulator sim, Vector3 me, CancellationToken cancellationToken)
    {
        if (FindParcel(client, sim, me) is { } known) return known;

        var tcs = new TaskCompletionSource<Parcel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnParcel(object? sender, ParcelPropertiesEventArgs e)
        {
            if (e.Simulator?.Handle == sim.Handle && e.Parcel is { } p && Contains(p, me)) tcs.TrySetResult(p);
        }
        client.Parcels.ParcelProperties += OnParcel;
        try
        {
            client.Parcels.RequestParcelProperties(sim, me.Y + 1, me.X + 1, me.Y - 1, me.X - 1, 0, false);
            var done = await Task.WhenAny(tcs.Task, Task.Delay(PropertiesWait, cancellationToken));
            return done == tcs.Task ? tcs.Task.Result : FindParcel(client, sim, me);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "Parcel lookup failed");
            return null;
        }
        finally
        {
            client.Parcels.ParcelProperties -= OnParcel;
        }
    }

    private static Parcel? FindParcel(GridClient client, Simulator sim, Vector3 me)
    {
        try
        {
            var localId = client.Parcels.GetParcelLocalID(sim, me);
            if (localId != 0 && sim.Parcels.TryGetValue(localId, out var byMap)) return byMap;
        }
        catch (Exception)
        {
            // The parcel map isn't downloaded yet.
        }
        return client.Parcels.CurrentParcel is { } current && Contains(current, me) ? current : null;
    }

    private static bool Contains(Parcel p, Vector3 me) =>
        me.X >= p.AABBMin.X && me.X <= p.AABBMax.X && me.Y >= p.AABBMin.Y && me.Y <= p.AABBMax.Y;
}
