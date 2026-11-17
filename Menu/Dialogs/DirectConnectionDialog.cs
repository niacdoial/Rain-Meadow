using System;
using System.Linq;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using RainMeadow.UI.Components;
using UnityEngine;

namespace RainMeadow.UI.Dialogs;

public class DirectConnectionDialog : InputDialog
{
    public OpTextBox passwordBox;
    public OpComboBox domainDropDown;

    public event Action<NetworkDomain.NetworkDomainType, string, string?>? OnDirectConnectConfirm;

    public DirectConnectionDialog(ProcessManager manager, Vector2 size)
        : base(manager, "Direct Connection", size)
    {
        MenuLabel passwordLabel = new(
            this,
            dialogPage,
            "Password",
            UIUtils.ScreenCenter(manager) - new Vector2(0, 40),
            Vector2.zero,
            false
        );

        passwordBox = new OpTextBox(
            new Configurable<string>(""),
            UIUtils.ScreenCenter(manager) - new Vector2(80, 74),
            160f
        )
        {
            accept = OpTextBox.Accept.StringASCII,
            allowSpace = true,
            greyedOut = true,
        };

        SimplerCheckbox hasPasswordCheckBox = new(
            this,
            dialogPage,
            // FIXME: adjust position
            UIUtils.ScreenCenter(manager) - new Vector2(114, 110),
            0,
            ""
        );
        hasPasswordCheckBox.OnClick += (checkBox) => passwordBox.greyedOut = !passwordBox.greyedOut;


        domainDropDown = new OpComboBox2(
            new Configurable<NetworkDomain.NetworkDomainType>(NetworkDomain.currentDomain),
            UIUtils.ScreenCenter(manager) - new Vector2(55, 74),
            160f - 35f,
            NetworkDomain.supportedDomains
                .Where(x => NetworkDomain.instances[x].canDirectConnect)
                .Select(x => new ListItem(x.value, Utils.Translate(x.value)))
                .ToList()
        ) { colorEdge = MenuColorEffect.rgbWhite };

        new UIelementWrapper(tabWrapper, passwordBox);
        new UIelementWrapper(tabWrapper, domainDropDown);
        
        dialogPage.subObjects.AddRange([passwordLabel, hasPasswordCheckBox]);

        OnConfirm += (ip) =>
            OnDirectConnectConfirm?.Invoke(
                new NetworkDomain.NetworkDomainType(domainDropDown.value, false),
                ip,
                hasPasswordCheckBox.Checked ? passwordBox.value : null
            );
    }
}
