using Content.Shared._EE.Silicon.Components;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Robust.Shared.Serialization;
using Content.Shared.Movement.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Timing;

namespace Content.Shared._Floof.Silicon.IPC;

public abstract partial class SharedSiliconSystem : EntitySystem
{
    [Dependency] protected AlertsSystem _alertsSystem = default!;
    [Dependency] protected ItemSlotsSystem _itemSlots = default!;
    [Dependency] protected IGameTiming _timing = default!;
    [Dependency] protected ILogManager _logManager = default!;
    [Dependency] protected PowerCellSystem _powerCell = default!;
    [Dependency] protected MobStateSystem _mobState = default!;
    protected ISawmill _sawmill = default!;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("silicon");
        SubscribeLocalEvent<SiliconComponent, MobStateChangedEvent>(OnMobStateChanged);
        InitializeMovement();
    }

    private void OnMobStateChanged(EntityUid uid, SiliconComponent component, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical) //Skip critical and go straight to dead.
        {

            _mobState.ChangeMobState(uid, MobState.Dead, args.Component);
        }
    }


    protected virtual void OnUpdate(EntityUid uid, SiliconComponent siliconBody)
    {
        var chargeState = GetChargeState(uid, siliconBody);
        if (chargeState != siliconBody.ChargeState)
        {
            siliconBody.ChargeState = chargeState;

            var movementState = GetMovementModifierState(uid, siliconBody);
            if (movementState != siliconBody.MovementState)
            {
                siliconBody.MovementState = movementState;
                _movement.RefreshMovementSpeedModifiers(uid);
            }

            RaiseLocalEvent(uid, new SiliconChargeStateUpdateEvent(siliconBody.ChargeState));
        }

    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;
        _nextUpdate = now + TimeSpan.FromSeconds(1);


        var query = EntityQueryEnumerator<SiliconComponent>();
        while (query.MoveNext(out var uid, out var siliconBody))
        {
            _powerCell.SetDrawEnabled(siliconBody.Owner, true);

            OnUpdate(uid, siliconBody);
            Dirty(uid, siliconBody);
        }
    }
}

public enum SiliconType
{
    Player,
    GhostRole,
    Npc,
}

/// <summary>
///     Event raised when a Silicon's charge state needs to be updated.
/// </summary>
[Serializable, NetSerializable]
public sealed class SiliconChargeStateUpdateEvent : EntityEventArgs
{
    public int ChargePercent { get; }

    public SiliconChargeStateUpdateEvent(int chargePercent)
    {
        ChargePercent = chargePercent;
    }
}
