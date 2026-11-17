using Menu;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RainMeadow
{
    public class DialogAsyncWait : Dialog
    {
        public DialogAsyncWait(Menu.Menu menu, string description, Vector2 size) : this(menu.manager, description, size) { }
        public DialogAsyncWait(ProcessManager manager, string description, Vector2 size)
            : base(description, size, manager)
        {
            loadingSpinner = new AtlasAnimator(0, new Vector2((float)((int)(pos.x + size.x / 2f)) - HorizontalMoveToGetCentered(manager), (float)((int)(pos.y + size.y / 2f - 32f))), "sleep", "sleep", 20, true, false);
            loadingSpinner.animSpeed = 0.25f;
            loadingSpinner.specificSpeeds = new Dictionary<int, float>();
            loadingSpinner.specificSpeeds[1] = 0.0125f;
            loadingSpinner.specificSpeeds[13] = 0.0125f;
            loadingSpinner.AddToContainer(container);
        }

        public override void Update()
        {
            base.Update();
            loadingSpinner.Update();
        }

        public virtual void RemoveSprites()
        {
            loadingSpinner.RemoveFromContainer();
        }

        public void SetText(string caption)
        {
            descriptionLabel.text = caption;
        }

        private readonly AtlasAnimator loadingSpinner;
    }
}
