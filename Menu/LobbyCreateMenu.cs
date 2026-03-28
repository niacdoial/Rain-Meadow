// HACK
using BepInEx;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using RainMeadow.UI.Components;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Menu.Remix.MixedUI.ValueTypes;

namespace RainMeadow;

public class LobbyCreateMenu : SmartMenu
{
    private OpComboBox2 visibilityDropDown;
    private UIelementWrapper visibilityWrapper;
    private OpTypeBox lobbyLimitNumberTextBox;
    private int maxPlayerCount;
    private OpCheckBox? lobbyPinnedCheckBox;
    private OpCheckBox? lobbyEventGags;

    private OpComboBox2 meadowTimelineDropdown;

    private SimplerButton createButton;
    private OpComboBox2 modeDropDown;
    private ProperlyAlignedMenuLabel modeDescriptionLabel;
    private OpComboBox2 domainDropdown;
    private OpTypeBox lobbyServerOverrideBox;
    private UIelementWrapper lobbyServerOverrideWrapper;
    private ProperlyAlignedMenuLabel timelineDescription;
    private OpTypeBox passwordInputBox;
    private MenuDialogBox? popupDialog;
    public override MenuScene.SceneID GetScene => ModManager.MMF ? manager.rainWorld.options.subBackground : MenuScene.SceneID.Landscape_SU;

    public string? meadowTimeline;
    public LobbyCreateMenu(ProcessManager manager) : base(manager, RainMeadow.Ext_ProcessID.LobbyCreateMenu)
    {
        // title at the top
        this.scene.AddIllustration(new MenuIllustration(this, this.scene, "illustrations/rainmeadowtitle", Utils.GetMeadowTitleFileName(true), new Vector2(-2.99f, 265.01f), true, false));
        this.scene.AddIllustration(new MenuIllustration(this, this.scene, "illustrations/rainmeadowtitle", Utils.GetMeadowTitleFileName(false), new Vector2(-2.99f, 265.01f), true, false));
        this.scene.flatIllustrations[this.scene.flatIllustrations.Count - 1].sprite.shader = this.manager.rainWorld.Shaders["MenuText"];

        // creation button in bottom right
        createButton = new SimplerButton(this, mainPage, Translate("CREATE!"), new Vector2(1056f, 50f), new Vector2(110f, 30f));
        createButton.OnClick += CreateLobby;
        mainPage.subObjects.Add(createButton);

        // game mode selection in top center
        var where = new Vector2(523f, 550);
        var modeLabel = new ProperlyAlignedMenuLabel(this, mainPage, Translate("Mode:"), where, new Vector2(200, 20f), false);
        mainPage.subObjects.Add(modeLabel);
        where.x += 80;
        modeDropDown = new OpComboBox2(
            new Configurable<OnlineGameMode.OnlineGameModeType>(OnlineGameMode.OnlineGameModeType.Meadow),
            where,
            160,
            OpResourceSelector.GetEnumNames(null, typeof(OnlineGameMode.OnlineGameModeType))
              .Select(li => { li.displayName = Translate(li.displayName); return li; })
              .ToList()
        ) { colorEdge = MenuColorEffect.rgbWhite };
        modeDropDown.OnChanged += UpdateModeDescription;

        new UIelementWrapper(this.tabWrapper, modeDropDown);
        where.x -= 80;
        where.y -= 35;
        modeDescriptionLabel = new ProperlyAlignedMenuLabel(this, mainPage, "", where, new Vector2(0, 20f), false);
        mainPage.subObjects.Add(modeDescriptionLabel);

        where.y -= 45;
        mainPage.subObjects.Add(
            new ProperlyAlignedMenuLabel(this, mainPage, Translate("Password:"), where, new Vector2(200, 20f), false)
        );
        where.x += 160;
        passwordInputBox = new OpTypeBox(new Configurable<string>(""), where, 160f)
        {
            accept = OpTextBox.Accept.StringASCII,
            allowSpace = true,
            defaultValue = "",
            description = Utils.Translate("Lobby Password"),
            password = RainMeadow.rainMeadowOptions.StreamerMode.Value == RainMeadowOptions.StreamMode.Me || RainMeadow.rainMeadowOptions.StreamerMode.Value == RainMeadowOptions.StreamMode.Everyone,
        };
        passwordInputBox.PosX = modeDropDown.pos.x;
        passwordInputBox.label.text = Utils.Translate("Password");
        new UIelementWrapper(this.tabWrapper, passwordInputBox);

        // lobby limit setting in bottom center
        where.x -= 160;
        where.y -= 45;
        var limitNumberLabel = new ProperlyAlignedMenuLabel(this, mainPage, Translate("Player max:"), where, new Vector2(400, 20f), false);
        mainPage.subObjects.Add(limitNumberLabel);
        where.x += 80;
        where.y -= 5;
        lobbyLimitNumberTextBox = new OpTypeBox(new Configurable<int>(maxPlayerCount = 4), where, 160f)
        {
            accept = OpTextBox.Accept.Int,
            maxLength = 2,
            description = Utils.Translate("Maximum number of players that can be in the lobby (up to 32)"),
        };
        new UIelementWrapper(this.tabWrapper, lobbyLimitNumberTextBox);
        where.y += 5;
        where.x -= 80;

        //Timeline selection dropdown
        where.y -= 45;
        timelineDescription = new ProperlyAlignedMenuLabel(this, mainPage, Translate("Timeline:"), where, new Vector2(200, 20f), false);
        mainPage.subObjects.Add(timelineDescription);
        where.x += 80;
        where.y -= 5;
        meadowTimeline = SlugcatStats.Name.White.value;

        var filteredList = OpResourceSelector.GetEnumNames(null, typeof(SlugcatStats.Name))
            .Where(li =>
            {
                var name = (SlugcatStats.Name)ExtEnumBase.Parse(typeof(SlugcatStats.Name), li.name, false);
                // Include eg boi
                return !SlugcatStats.HiddenOrUnplayableSlugcat(name) || (ModManager.MSC && name == MoreSlugcats.MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel);
            })
            .Select(li =>
            {
                li.displayName = Translate(li.displayName);
                return li;
            })
            .ToList();

        meadowTimelineDropdown = new OpComboBox2(
            new Configurable<string>(meadowTimeline),
            where,
            160,
            filteredList
        )
        { colorEdge = MenuColorEffect.rgbWhite };
        meadowTimelineDropdown.OnChanged += UpdateMeadowTimeline;
        new UIelementWrapper(this.tabWrapper, meadowTimelineDropdown);
        where.x -= 80;

        if (NetworkDomain.instances.OfType<NetworkDomain>().Any(x => x.IsTrustedCommunity(OnlineManager.mePlayer.id)))
        {
            where.y -= 45;
            mainPage.subObjects.Add(new ProperlyAlignedMenuLabel(this, mainPage, Translate("Pinned:"), where, new Vector2(400, 20f), false));
            where.x += 80;
            where.y -= 5;
            lobbyPinnedCheckBox = new OpCheckBox(new Configurable<bool>(false), where);
            new UIelementWrapper(this.tabWrapper, lobbyPinnedCheckBox);
            where.y += 5;
        }
        if (SpecialEvents.IsSpecialEvent)
        {
            where.y -= 45;
            mainPage.subObjects.Add(new ProperlyAlignedMenuLabel(this, mainPage, Translate("Event Gags:"), where, new Vector2(400, 20f), false));
            where.x += 80;
            where.y -= 5;
            lobbyEventGags = new OpCheckBox(new Configurable<bool>(false), where);
            new UIelementWrapper(this.tabWrapper, lobbyEventGags);
            where.x -= 80;
            where.y += 5;
        }


        where.y -= 85;
        limitNumberLabel = new ProperlyAlignedMenuLabel(this, mainPage, Translate("Domain:"), where, new Vector2(200, 20f), false);
        mainPage.subObjects.Add(limitNumberLabel);
        where.x += 80;
        where.y -= 5;
        domainDropdown = new OpComboBox2(new Configurable<string>(
                NetworkDomain.supportedDomains.Last().value), where, 160f - 35f, NetworkDomain.supportedDomains.Select(x => new ListItem(x.value, Utils.Translate(x.value))).ToList()) { colorEdge = MenuColorEffect.rgbWhite };
        new UIelementWrapper(this.tabWrapper, domainDropdown);
        where.x -= 80;


        where.y -= 45;
        var visibilityLabel = new ProperlyAlignedMenuLabel(this, mainPage, Translate("Visibility:"), where, new Vector2(200, 20f), false);
        mainPage.subObjects.Add(visibilityLabel);
        where.x += 80;
        visibilityDropDown = new OpComboBox2(new Configurable<NetworkDomain.LobbyVisibility>(NetworkDomain.LobbyVisibility.Public), where, 160, OpResourceSelector.GetEnumNames(null, typeof(NetworkDomain.LobbyVisibility)).Select(li => { li.displayName = Translate(li.displayName); return li; }).ToList()) { colorEdge = MenuColorEffect.rgbWhite };
        visibilityWrapper = new UIelementWrapper(this.tabWrapper, visibilityDropDown);

        where.x += 200;
        visibilityLabel = new ProperlyAlignedMenuLabel(this, mainPage, Translate("(Steam domain only)"), where, new Vector2(200, 20f), false);
        mainPage.subObjects.Add(visibilityLabel);
        where.x -= 200;

        where.y -= 45;
        where.x -= 80;
        var lobbyServerOverrideLabel = new ProperlyAlignedMenuLabel(this, mainPage, Translate("Lobby server:"), where, new Vector2(200, 20f), false);
        mainPage.subObjects.Add(lobbyServerOverrideLabel);
        where.x += 80;
        lobbyServerOverrideBox = new OpTypeBox(new Configurable<string>(""), where, 160f)
        {
            accept = OpTextBox.Accept.StringASCII,
            allowSpace = true,
            defaultValue = "",
            maxLength = 100,
            description = Utils.Translate("In Router network, specify the endpoint (pubkey@ip:port) for a custom lobby server instead of submitting the lobby to the global matchmaking server"),
            password = RainMeadow.rainMeadowOptions.StreamerMode.Value == RainMeadowOptions.StreamMode.Me || RainMeadow.rainMeadowOptions.StreamerMode.Value == RainMeadowOptions.StreamMode.Everyone,
        };
        lobbyServerOverrideBox.PosX = modeDropDown.pos.x;
        lobbyServerOverrideBox.label.text = Utils.Translate("[default: Meadow servers]");
        lobbyServerOverrideWrapper = new UIelementWrapper(this.tabWrapper, lobbyServerOverrideBox);

        where.x += 200;
        lobbyServerOverrideLabel = new ProperlyAlignedMenuLabel(this, mainPage, Translate("(Router domain only)"), where, new Vector2(200, 20f), false);
        mainPage.subObjects.Add(lobbyServerOverrideLabel);
        where.x -= 200;

        // display version
        where.x += 80;
        MenuLabel versionLabel = new MenuLabel(this, pages[0], $"{Utils.Translate("Rain Meadow Version:")} {RainMeadow.MeadowVersionStr}", new Vector2((1336f - manager.rainWorld.screenSize.x) / 2f + 20f, manager.rainWorld.screenSize.y - 768f), new Vector2(200f, 20f), false, null);
        versionLabel.size = new Vector2(versionLabel.label.textRect.width, versionLabel.size.y);
        mainPage.subObjects.Add(versionLabel);

        if (backObject is SimplerButton backButton) backButton.menuLabel.text = Utils.Translate("CANCEL");

        UpdateModeDescription();
        CreateElementBindings();
        NetworkDomain.OnLobbyJoined += OnLobbyJoined;
    }

    public void OnLobbyJoined(bool ok, string error = "")
    {
        if (!ok)
        {
            ShowErrorDialog(error);
        }
        else
        {
            OnlineManager.lobby.eventGags = lobbyEventGags?.GetValueBool() ?? false;
            OnlineManager.lobby.meadowTimeline = this.meadowTimeline;
        }
    }

    public override void Init()
    {
        base.Init();
        selectedObject = modeDropDown.wrapper;
    }

    private void UpdateModeDescription()
    {
        modeDescriptionLabel.text = Custom.ReplaceLineDelimeters(Translate(OnlineGameMode.OnlineGameModeType.descriptions[new OnlineGameMode.OnlineGameModeType(modeDropDown.value)]));
        if (modeDropDown.value == OnlineGameMode.OnlineGameModeType.Meadow.value)
        {
            meadowTimelineDropdown.greyedOut = false;
        }
        else
        {
            meadowTimelineDropdown.greyedOut = true;
            meadowTimeline = "";
        }

    }

    private void UpdateMeadowTimeline()
    {
        meadowTimeline = SlugcatStats.SlugcatToTimeline(new SlugcatStats.Name(meadowTimelineDropdown.value)).value;
        RainMeadow.Debug($"Selected Meadow Timeline: {meadowTimeline}");
    }

    public void CreateElementBindings()
    {
        //Column; enforce element order, and fix/adjust left/right binds.
        List<MenuObject> VerticalElements = [modeDropDown.wrapper, visibilityDropDown.wrapper, passwordInputBox.wrapper, lobbyLimitNumberTextBox.wrapper, meadowTimelineDropdown.wrapper];
        if (lobbyPinnedCheckBox != null) { VerticalElements.Add(lobbyPinnedCheckBox.wrapper); }
        if (lobbyEventGags != null) { VerticalElements.Add(lobbyEventGags.wrapper); }
        Extensions.TrySequentialMutualBind(this, VerticalElements, bottomTop: true, loopLastIndex: true, reverseList: true);
        Extensions.TryMassBind(VerticalElements, backObject, left: true);
        Extensions.TryMassBind(VerticalElements, createButton, right: true);
        //Bottom row; enforce element order and fix/adjust up/down binds.
        List<MenuObject> BottomRowElements = [backObject, createButton];
        Extensions.TryMassBind(BottomRowElements, VerticalElements.Last(), top: true);
        Extensions.TryMassBind(BottomRowElements, VerticalElements.First(), bottom: true);
        Extensions.TryMutualBind(this, backObject, createButton, leftRight: true);
    }

    private void CreateLobby(SimplerButton obj)
    {
        ShowLoadingDialog("Creating lobby...");
        ApplyLobbyLimit();
        RainMeadow.Debug($"Creating a lobby with a max player limit of {maxPlayerCount}");
        RequestLobbyCreate();
    }

    private void ApplyLobbyLimit()
    {
        maxPlayerCount = lobbyLimitNumberTextBox.valueInt;
        if (lobbyLimitNumberTextBox.valueInt > 32) lobbyLimitNumberTextBox.valueInt = 32;
        if (lobbyLimitNumberTextBox.valueInt < 2) lobbyLimitNumberTextBox.valueInt = 2;
    }

    private void RequestLobbyCreate()
    {
        var domain = (NetworkDomain.NetworkDomainType)ExtEnumBase.Parse(typeof(NetworkDomain.NetworkDomainType), domainDropdown.value, true);

        RainMeadow.DebugMe();
        Enum.TryParse<NetworkDomain.LobbyVisibility>(visibilityDropDown.value, out var value);
        string? password = passwordInputBox.value.IsNullOrWhiteSpace() ? null : passwordInputBox.value;
        try
        {
            if (domain == NetworkDomain.NetworkDomainType.Router)
            {
                RainMeadow.Debug("override value: " + lobbyServerOverrideBox.value);
                NetworkDomain.Router.PreconfigureLobbyServerForCreation(lobbyServerOverrideBox.value);
            }
            NetworkDomain.instances[domain].CreateLobby(value, modeDropDown.value, password, maxPlayerCount, this.lobbyPinnedCheckBox?.GetValueBool() ?? false);
        }
        catch (Exception except) {
            ShowErrorDialog(except.Message);
        }
    }

    private void ShowLoadingDialog(string text)
    {
        if (popupDialog != null) HideDialog();

        text = Utils.Translate(text);

        popupDialog = new DialogBoxAsyncWait(this, mainPage, text, new Vector2(manager.rainWorld.options.ScreenSize.x / 2f - 240f + (1366f - manager.rainWorld.options.ScreenSize.x) / 2f, 224f), new Vector2(480f, 320f));
        mainPage.subObjects.Add(popupDialog);
    }

    private void ShowErrorDialog(string error)
    {
        if (popupDialog != null) HideDialog();

        popupDialog = new DialogBoxNotify(this, mainPage, error, "HIDE_DIALOG", new Vector2(manager.rainWorld.options.ScreenSize.x / 2f - 240f + (1366f - manager.rainWorld.options.ScreenSize.x) / 2f, 224f), new Vector2(480f, 320f));
        mainPage.subObjects.Add(popupDialog);
    }

    private void HideDialog()
    {
        if (popupDialog == null) return;

        mainPage.RemoveSubObject(popupDialog);
        popupDialog.RemoveSprites();
        popupDialog = null;
    }

    public override void Singal(MenuObject sender, string message)
    {
        base.Singal(sender, message);
        if (message == "HIDE_DIALOG") HideDialog();
    }

    public override void ShutDownProcess()
    {
        NetworkDomain.OnLobbyJoined -= OnLobbyJoined;
        base.ShutDownProcess();
    }
}
