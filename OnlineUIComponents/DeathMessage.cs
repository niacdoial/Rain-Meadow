using MoreSlugcats;
using System;

namespace RainMeadow;

public static class DeathMessage
{
    public static void EnvironmentalDeathMessage(Player player, DeathType cause)
    {
        try
        {
            if (player == null || player.dead)
            {
                return;
            }
            var t = player.abstractPhysicalObject.GetOnlineObject().owner.id.name;
            switch (cause)
            {
                default:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("died."));
                    break;
                case DeathType.Rain:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("was crushed by the rain."));
                    break;
                case DeathType.Abyss:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("fell into the abyss."));
                    break;
                case DeathType.Drown:
                    if (player.grabbedBy.Count > 0)
                    {
                        ChatLogManager.LogMessage("", t + " " + Utils.Translate("was drowned by") + player.grabbedBy[0].grabber.Template.name);
                        break;
                    }
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("drowned."));
                    break;
                case DeathType.FallDamage:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("hit the ground too hard."));
                    break;
                case DeathType.Oracle:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("was killed through unknown means."));
                    break;
                case DeathType.Burn:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("tried to swim in burning liquid."));
                    break;
                case DeathType.PyroDeath:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("spontaneously combusted."));
                    break;
                case DeathType.Freeze:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("froze to death."));
                    break;
                case DeathType.WormGrass:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("was swallowed by the grass."));
                    break;
                case DeathType.WallRot:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("was swallowed by the walls."));
                    break;
                case DeathType.Electric:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("was electrocuted."));
                    break;
                case DeathType.DeadlyLick:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("licked the power."));
                    break;
                case DeathType.Coalescipede:
                    ChatLogManager.LogMessage("", t + " " + Utils.Translate("was consummed by the swarm."));
                    break;
            }
        }
        catch (Exception e)
        {
            RainMeadow.Error("Error displaying death message. " + e);
        }
    }
    public static void PlayerKillPlayer(Player killer, Player target)
    {
        try
        {
            var k = killer.abstractPhysicalObject.GetOnlineObject().owner.id.name;
            var t = target.abstractPhysicalObject.GetOnlineObject().owner.id.name;
            ChatLogManager.LogMessage("", t + " " + Utils.Translate("was slain by") + $" {k}.");
        }
        catch (Exception e)
        {
            RainMeadow.Error("Error displaying death message. " + e);
        }
    }

    public static void CreatureKillPlayer(Creature killer, Player target)
    {
        if (killer is Player)
        {
            return;
        }

        try
        {
            var k = killer.Template.name;
            var t = target.abstractPhysicalObject.GetOnlineObject().owner.id.name;
            if (killer.Template.TopAncestor().type == CreatureTemplate.Type.Centipede)
            {
                ChatLogManager.LogMessage("", t + " " + Utils.Translate("was zapped by a") + $" {k}.");
            } 
            else
            {
                ChatLogManager.LogMessage("", t + " " + Utils.Translate("was slain by a") + $" {k}.");
            }
        }
        catch (Exception e)
        {
            RainMeadow.Error("Error displaying death message. " + e);
        }
    }

    public static void PlayerKillCreature(Player killer, Creature target)
    {
        if (target is Player)
        {
            PlayerKillPlayer(killer, (Player)target);
            return;
        }
        try
        {
            var k = killer.abstractPhysicalObject.GetOnlineObject().owner.id.name;
            var t = target.Template.name;
            if (target.TotalMass > 0.2f) ChatLogManager.LogMessage("", t + " " + Utils.Translate("was slain by") + $" {k}.");
        }
        catch (Exception e)
        {
            RainMeadow.Error("Error displaying death message. " + e);
        }
    }

    public static void PlayerDeathEvent(Player player, Type sourceType, object source)
    {
        if (OnlineManager.lobby == null || OnlineManager.lobby.gameMode is MeadowGameMode) return;
        if (player.dead) return;
        switch(source)
        {
            case ZapCoil:
                EnvironmentalDeathMessage(player, DeathType.Electric);
                break;
            case WormGrass.WormGrassPatch:
                EnvironmentalDeathMessage(player, DeathType.WormGrass);
                break;
            case SSOracleBehavior:
                EnvironmentalDeathMessage(player, DeathType.Oracle);
                break;
            case DaddyCorruption.EatenCreature:
                EnvironmentalDeathMessage(player, DeathType.WallRot);
                break;
            case Player.Tongue:
                EnvironmentalDeathMessage(player, DeathType.DeadlyLick);
                break;
        }
    }

    public static void CreatureDeath(Creature crit)
    {
        if (crit.killTag != null && crit.killTag.realizedCreature != null)
        {
            if (crit.killTag.realizedCreature is Player && !RainMeadow.isArenaMode(out var _))
            {
                PlayerKillCreature(crit.killTag.realizedCreature as Player, crit);
            }
            else if (crit is Player)
            {
                CreatureKillPlayer(crit.killTag.realizedCreature, crit as Player);
            }
        }
        else
        {
            // (try to) Determine the cause of death if it wasn't from a kill.
            // Will probably be way better to have this information sent by the client that actually died for accuracy but this should work good enough for now.
            if (crit is Player player)
            {
                if (player.drown >= 1f)
                {
                    EnvironmentalDeathMessage(player, DeathType.Drown);
                    return;
                }
                if (player.Hypothermia >= 1f)
                {
                    EnvironmentalDeathMessage(player, DeathType.Freeze);
                    return;
                }
                if (player.rainDeath > 1f)
                {
                    EnvironmentalDeathMessage(player, DeathType.Rain);
                    return;
                }

                if (ModManager.MSC && player.SlugCatClass == MoreSlugcatsEnums.SlugcatStatsName.Artificer)
                {
                    if (player.airInLungs <= Player.PyroDeathThreshold(player.room.game))
                    {
                        EnvironmentalDeathMessage(player, DeathType.PyroDeath);
                        return;
                    }
                }

                if (player.Submersion > 0.2f && player.room.waterObject != null && player.room.waterObject.WaterIsLethal && !player.abstractCreature.lavaImmune)
                {
                    EnvironmentalDeathMessage(player, DeathType.Burn);
                    return;
                }

                if (player.grabbedBy.Count > 0)
                {
                    float spiders = 0f;
                    for (int i = 0; i < player.grabbedBy.Count; i++)
                    {
                        if (player.grabbedBy[i].grabber is Spider)
                        {
                            spiders+= player.grabbedBy[i].grabber.TotalMass;
                        }
                    }
                    if (spiders >= player.TotalMass)
                    {
                        EnvironmentalDeathMessage(player, DeathType.Coalescipede);
                    }
                }
            }
        }
    }
        public enum DeathType
    {
        Invalid,
        Rain,
        Abyss,
        Drown,
        FallDamage,
        Oracle,
        Burn,
        PyroDeath,
        Freeze,
        WormGrass,
        WallRot,
        Electric,
        DeadlyLick,
        Coalescipede
    }
}
