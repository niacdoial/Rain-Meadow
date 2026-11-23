using System;
using System.Linq;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using RainMeadow.UI.Components.Patched;
using UnityEngine;

namespace RainMeadow.UI.Menus.Panels;

class LobbySelectNetworkPanel : PositionedMenuObject
{
    public event Action? OnDirectConnectButtonClick;
    public event Action? OnDomainChanged;

    public SimplerButton directConnectButton;

    public LobbySelectNetworkPanel(Menu.Menu menu, MenuObject owner, Vector2 pos)
        : base(menu, owner, pos)
    {
        MenuTabWrapper tabWrapper = new(menu, this);

        directConnectButton = new SimplerButton(
            menu,
            this,
            menu.Translate("Direct Connect"),
            new Vector2(20, 445),
            new Vector2(160f, 30f),
            menu.Translate("Directly connect to a local IP")
        );
        directConnectButton.buttonBehav.greyedOut = NetworkDomain.currentInstance.canDirectConnect;
        directConnectButton.OnClick += (btn) => OnDirectConnectButtonClick?.Invoke();

        ProperlyAlignedMenuLabel domainLabel = new(
            menu,
            this,
            menu.Translate("Lobby Domain"),
            new Vector2(20, 415),
            new Vector2(200f, 20f),
            false
        );

        OpComboBox2 domainComboBox = new(
            new Configurable<NetworkDomain.NetworkDomainType>(
                NetworkDomain.currentDomain
            ),
            new Vector2(20, 385),
            125f,
            [
                .. NetworkDomain.supportedDomains.Select(x => new ListItem(
                    x.value,
                    menu.Translate(x.value)
                )),
            ]
        );
        domainComboBox.OnChange += () =>
        {
            NetworkDomain.currentDomain = new NetworkDomain.NetworkDomainType(
                domainComboBox.value,
                false
            );
            OnDomainChanged?.Invoke();
        };

        new PatchedUIelementWrapper(tabWrapper, domainComboBox);
        subObjects.AddRange([tabWrapper, directConnectButton, domainLabel]);
    }

    public override void Update()
    {
        base.Update();
        // FIXME: as above: this looks wrong
        directConnectButton.buttonBehav.greyedOut = NetworkDomain.currentInstance.canDirectConnect;
    }
}
