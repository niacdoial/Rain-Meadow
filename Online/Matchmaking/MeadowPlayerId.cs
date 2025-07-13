using System;
using Menu;

namespace RainMeadow
{
#if IS_SERVER  // let's not bring the serialization into the server
    public abstract class MeadowPlayerId : IEquatable<MeadowPlayerId>
#else
    public abstract class MeadowPlayerId : IEquatable<MeadowPlayerId>, Serializer.ICustomSerializable
#endif
    {
        public string name;

        public virtual string GetPersonaName() { return name; }
#if !IS_SERVER
        public virtual void OpenProfileLink() {
            OnlineManager.instance.manager.ShowDialog(new DialogNotify(Utils.Translate("This player does not have a profile."), OnlineManager.instance.manager, null));
        }
#endif
        public virtual bool canOpenProfileLink { get => false; }

        protected MeadowPlayerId() { }
        protected MeadowPlayerId(string name)
        {
            this.name = name;
        }

#if !IS_SERVER  // let's not bring the serialization into the server
        public abstract void CustomSerialize(Serializer serializer);
#endif
        public abstract bool Equals(MeadowPlayerId other);
        public override bool Equals(object obj)
        {
            return Equals(obj as MeadowPlayerId);
        }
        public abstract override int GetHashCode();
        public override string ToString()
        {
            return name;
        }
        public static bool operator ==(MeadowPlayerId lhs, MeadowPlayerId rhs)
        {
            return lhs is null ? rhs is null : lhs.Equals(rhs);
        }
        public static bool operator !=(MeadowPlayerId lhs, MeadowPlayerId rhs) => !(lhs == rhs);
    }
}
