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

        public void RemoveSprites()
        {
            loadingSpinner.RemoveFromContainer();
        }

        public void SetText(string caption)
        {
            descriptionLabel.text = caption;
        }

        private readonly AtlasAnimator loadingSpinner;
    }

    public class DialogAsyncWaitCancellable : DialogAsyncWait
    {
        public readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public DialogAsyncWaitCancellable(Menu.Menu menu, string description, Vector2 size) : this(menu.manager, description, size) { }
        public DialogAsyncWaitCancellable(ProcessManager manager, string description, Vector2 size)
            : base(manager, description, size)
        {                                       
            actionButton = new SimpleButton(this, pages[0], Translate("Cancel"), "CANCEL", new Vector2(pos.x + (size.x - 110f) * 0.5f, pos.y + Mathf.Max(size.y * 0.04f, 7f)), new Vector2(110f, 30f));
            pages[0].subObjects.Add(actionButton);
        }

        public SimpleButton actionButton;
        public float timeOut;
        public override void Update()
        {
            base.Update();
            if (actionButton != null)
            {
                timeOut -= 0.025f;
                if (timeOut < 0f)
                {
                    timeOut = 0f;
                    actionButton.buttonBehav.greyedOut = false;
                }
                else
                {
                    actionButton.buttonBehav.greyedOut = true;
                }
            }
        }


        public void Error(string error)
        {
            SetText(error);
            actionButton.menuLabel.text = Translate("OK");
            // TODO: set sprite to dead slugcat
        }

        public void Success(string message)
        {
            SetText(message);
            actionButton.menuLabel.text = Translate("OK");
            // TODO: set sprite to alive slugcat
        }


        public override void Singal(MenuObject sender, string message)
        {
            base.Singal(sender, message);
            if (message != null && message == "CANCEL")
            {
                cancellationTokenSource.Cancel();
                manager.StopSideProcess(this);
            }
        }
    }

}
