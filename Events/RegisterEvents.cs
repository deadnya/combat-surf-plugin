using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace CombatSurf;

public partial class CombatSurf
{
  private void RegisterEvents()
  {
    // Listeners
    RegisterListener<Listeners.OnMapStart>(OnMapStart);
    RegisterListener<Listeners.OnGameServerSteamAPIActivated>(OnGameServerSteamAPIActivated);
    RegisterListener<Listeners.OnEntityCreated>(OnEntityCreated);

    // Events
    RegisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull);
    RegisterEventHandler<EventPlayerHurt>(OnEventPlayerHurt);
    RegisterEventHandler<EventRoundStart>(PreOnEventRoundStart, HookMode.Pre);
    RegisterEventHandler<EventPlayerSpawn>(OnEventPlayerSpawn);
    RegisterEventHandler<EventBulletImpact>(PreOnBulletImpact, HookMode.Pre);
    RegisterEventHandler<EventPlayerShoot>(OnEventPlayerShoot);

    // VirtualFunctions
    VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(PreOnTakeDamage, HookMode.Pre);
  }
  private void UnRegisterEvents()
  {
    DeregisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull);
    DeregisterEventHandler<EventPlayerHurt>(OnEventPlayerHurt);
    DeregisterEventHandler<EventRoundStart>(PreOnEventRoundStart);
    DeregisterEventHandler<EventPlayerSpawn>(OnEventPlayerSpawn);
    VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(PreOnTakeDamage, HookMode.Pre);
  }

  // Listeners
  private void OnEntityCreated(CEntityInstance entity)
  {
    IncreaseAwpAmmo(entity);
  }
  private void OnMapStart(string mapName)
  {
    SetupServerSettings();
  }
  private void OnGameServerSteamAPIActivated()
  {

  }

  // Event
  private HookResult OnEventPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
  {
    CCSPlayerController player = @event.Userid!;
    CCSPlayerController attacker = @event.Attacker!;

    if (!player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected || !player.PlayerPawn.IsValid)
      return HookResult.Continue;

    player.PlayerPawn.Value!.VelocityModifier = 1;

    if (@event.Weapon == "awp" && @event.Health == 0)
    {
      SetHitEffetct(attacker);
    }

    var eventMsg = new EventOnPlayerHurt
    {
      @event = @event,
      info = info
    };
    _eventsManager.Publish(eventMsg);

    return HookResult.Continue;
  }

  private HookResult OnEventPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
  {
    CCSPlayerController? client = @event.Userid;

    if (!ClientIsValid(client))
      return HookResult.Continue;

    var eventMsg = new EventOnPlayerConnect
    {
      Name = client!.PlayerName,
      Slot = client.Slot,
      SteamId = client.SteamID.ToString()
    };
    _eventsManager.Publish(eventMsg);

    return HookResult.Continue;
  }

  private HookResult PreOnEventRoundStart(EventRoundStart @event, GameEventInfo info)
  {
    AddTimer(1.0f, SetupServerSettings);

    return HookResult.Continue;
  }

  private HookResult OnEventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
  {
    CCSPlayerController client = @event.Userid!;

    if (!ClientIsValid(client))
      return HookResult.Continue;

    Server.NextFrame(() =>
    {
      ColorizeModel(client);

      var player = _playerManager.GetPlayer(client);
      if (player != null)
      {
        player.SpawnAt = Server.CurrentTime;
        _gunManager.GivePlayerWeapon(player, player.LastSelectedGun);
      }
    });

    return HookResult.Continue;
  }

  private HookResult PreOnTakeDamage(DynamicHook hook)
  {
    OneShootAwp(hook);

    return HookResult.Continue;
  }

  private HookResult OnEventPlayerShoot(EventPlayerShoot @event, GameEventInfo info)
  {

    return HookResult.Continue;
  }

  public HookResult PreOnBulletImpact(EventBulletImpact @event, GameEventInfo info)
  {
    CCSPlayerController client = @event.Userid!;

    if (!ClientIsValidAndAlive(client))
      return HookResult.Continue;

    Vector BulletDestination = new Vector(@event.X, @event.Y, @event.Z);

    var weapon = client.Pawn.Value!.WeaponServices?.ActiveWeapon?.Value;
    if (weapon?.DesignerName != "weapon_awp")
      return HookResult.Continue;

    ApplyKnockback(client, BulletDestination);

    return HookResult.Continue;
  }
}
