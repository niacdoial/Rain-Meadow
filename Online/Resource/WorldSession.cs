﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RainMeadow
{
    public partial class WorldSession : OnlineResource
    {
        public Region region;
        public World world;
        public static ConditionalWeakTable<World, WorldSession> map = new();
        public Dictionary<string, RoomSession> roomSessions = new();

        public override World World => world;

        public WorldSession(Region region, Lobby lobby)
        {
            this.region = region;
            this.super = lobby;
        }

        public void BindWorld(World world)
        {
            this.world = world;
            map.Add(world, this);
        }

        protected override void AvailableImpl()
        {

        }

        protected override void ActivateImpl()
        {
            if (world == null) throw new InvalidOperationException("world not set");
            foreach (var room in world.abstractRooms)
            {
                var rs = new RoomSession(this, room);
                roomSessions.Add(room.name, rs);
                subresources.Add(rs);
            }
            foreach (var item in earlyApos)
            {
                ApoEnteringWorld(item);
            }
            earlyApos.Clear();
        }

        protected override void DeactivateImpl()
        {
            this.roomSessions.Clear();
            world = null;
        }

        protected override ResourceState MakeState(uint ts)
        {
            return new WorldState(this, ts);
        }

        public override string Id()
        {
            return region.name;
        }

        public override ushort ShortId()
        {
            return (ushort)region.regionNumber;
        }

        public override OnlineResource SubresourceFromShortId(ushort shortId)
        {
            return this.subresources[shortId - region.firstRoomIndex];
        }

        public class WorldState : ResourceWithSubresourcesState
        {
            public int cycleLength;
            public int timer;
            public int preTimer;
            public WorldState() : base() { }
            public WorldState(WorldSession resource, uint ts) : base(resource, ts) 
            {
                if (resource.world != null) {
                    RainCycle rainCycle = resource.world.rainCycle;
                    cycleLength = rainCycle.cycleLength;
                    timer = rainCycle.timer;
                    preTimer = rainCycle.preTimer;
                }
            }
            public override ResourceState ApplyDelta(ResourceState newState)
            {
                var newWorldState = (WorldState)newState;
                var value = (WorldState)base.ApplyDelta(newState);
                value.cycleLength = newWorldState.cycleLength;
                value.timer = newWorldState.timer;
                value.preTimer = newWorldState.preTimer;
                return value;
            }

            public override ResourceState Delta(ResourceState lastAcknoledgedState)
            {
                var delta = (WorldState)base.Delta(lastAcknoledgedState);
                delta.cycleLength = cycleLength;
                delta.timer = timer;
                delta.preTimer = preTimer;
                return delta;
            }
            public override ResourceState EmptyDelta() => new WorldState();
            public override void CustomSerialize(Serializer serializer)
            {
                base.CustomSerialize(serializer);
                serializer.Serialize(ref cycleLength);
                serializer.Serialize(ref timer);
                serializer.Serialize(ref preTimer);
            }
            public override void ReadTo(OnlineResource resource)
            {
                if (resource.isActive) {
                    var ws = (WorldSession)resource;
                    RainCycle cycle = ws.world.rainCycle;
                    cycle.preTimer = preTimer;
                    cycle.timer = timer;
                    cycle.cycleLength = cycleLength;
                }
                  
                base.ReadTo(resource);
            }
            public override StateType stateType => StateType.WorldState;
        }

        public override string ToString()
        {
            return "Region " + Id();
        }
    }
}
