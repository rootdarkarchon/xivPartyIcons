﻿using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Logging;
using PartyIcons.Api;
using PartyIcons.Entities;
using PartyIcons.Runtime;
using PartyIcons.Stylesheet;
using PartyIcons.Utils;

namespace PartyIcons.View
{
    public sealed class NameplateView : IDisposable
    {
        [PluginService] private ObjectTable ObjectTable { get; set; }

        private readonly Configuration    _configuration;
        private readonly PlayerStylesheet _stylesheet;
        private readonly RoleTracker      _roleTracker;
        private readonly PartyListHUDView _partyListHudView;

        private readonly IconSet _iconSet;

        public NameplateMode PartyMode  { get; set; }
        public NameplateMode OthersMode { get; set; }

        [PluginService] private ClientState ClientState { get; set; }

        public NameplateView(RoleTracker roleTracker, Configuration configuration, PlayerStylesheet stylesheet, PartyListHUDView partyListHudView)
        {
            _roleTracker = roleTracker;
            _configuration = configuration;
            _stylesheet = stylesheet;
            _partyListHudView = partyListHudView;
            _iconSet = new IconSet();
        }

        public void Dispose()
        {
        }

        public void SetupDefault(XivApi.SafeNamePlateObject npObject)
        {
            npObject.SetIconScale(1f);
            npObject.SetNameScale(0.5f);
        }

        public void SetupForPC(XivApi.SafeNamePlateObject npObject)
        {
            var nameScale = 0.75f;
            var iconScale = 1f;
            var iconOffset = new Vector2(0, 0);

            switch (GetModeForNameplate(npObject))
            {
                case NameplateMode.Default:
                case NameplateMode.SmallJobIcon:
                case NameplateMode.SmallJobIconAndRole:
                    SetupDefault(npObject);
                    return;

                case NameplateMode.Hide:
                    nameScale = 0f;
                    iconScale = 0f;
                    break;

                case NameplateMode.BigJobIcon:
                    nameScale = 0.75f;

                    switch (_configuration.SizeMode)
                    {
                        case NameplateSizeMode.Smaller:
                            iconOffset = new Vector2(9, 50);
                            iconScale = 1.5f;
                            break;

                        case NameplateSizeMode.Medium:
                            iconOffset = new Vector2(-12, 24);
                            iconScale = 3f;
                            break;

                        case NameplateSizeMode.Bigger:
                            iconOffset = new Vector2(-27, -12);
                            iconScale = 4f;
                            break;
                    }
                    break;

                case NameplateMode.BigJobIconAndPartySlot:
                    switch (_configuration.SizeMode)
                    {
                        case NameplateSizeMode.Smaller:
                            iconOffset = new Vector2(12, 62);
                            iconScale = 1.2f;
                            nameScale = 0.6f;
                            break;

                        case NameplateSizeMode.Medium:
                            iconOffset = new Vector2(-14, 41);
                            iconScale = 2.3f;
                            nameScale = 1f;
                            break;

                        case NameplateSizeMode.Bigger:
                            iconOffset = new Vector2(-32, 15);
                            iconScale = 3f;
                            nameScale = 1.5f;
                            break;
                    }
                    break;

                case NameplateMode.RoleLetters:
                    iconScale = 0f;
                    nameScale = _configuration.SizeMode switch
                    {
                        NameplateSizeMode.Smaller => 0.5f,
                        NameplateSizeMode.Medium  => 1f,
                        NameplateSizeMode.Bigger  => 1.5f,
                    };
                    break;
            }

            npObject.SetIconPosition((short)iconOffset.X, (short)iconOffset.Y);
            npObject.SetIconScale(iconScale);
            npObject.SetNameScale(nameScale);
        }

        public void NameplateDataForPC(
            XivApi.SafeNamePlateObject npObject,
            ref bool isPrefixTitle,
            ref bool displayTitle,
            ref IntPtr title,
            ref IntPtr name,
            ref IntPtr fcName,
            ref int iconID
        )
        {
            var uid = npObject.NamePlateInfo.Data.ObjectID.ObjectID;
            var mode = GetModeForNameplate(npObject);

            if (_configuration.HideLocalPlayerNameplate && uid == ClientState.LocalPlayer?.ObjectId)
            {
                switch (mode)
                {
                    case NameplateMode.Default:
                    case NameplateMode.Hide:
                    case NameplateMode.SmallJobIcon:
                    case NameplateMode.BigJobIcon:
                    case NameplateMode.BigJobIconAndPartySlot:
                        name = SeStringUtils.emptyPtr;
                        fcName = SeStringUtils.emptyPtr;
                        displayTitle = false;
                        iconID = 0;
                        return;

                    case NameplateMode.RoleLetters:
                        if (!_configuration.TestingMode && !npObject.NamePlateInfo.IsPartyMember())
                        {
                            name = SeStringUtils.emptyPtr;
                            fcName = SeStringUtils.emptyPtr;
                            displayTitle = false;
                            iconID = 0;
                            return;
                        }
                        break;
                }
            }

            var playerCharacter = ObjectTable.SearchById(uid) as PlayerCharacter;
            if (playerCharacter == null)
            {
                return;
            }

            var hasRole = _roleTracker.TryGetAssignedRole(playerCharacter.Name.TextValue, playerCharacter.HomeWorld.Id, out var roleId);
            switch (mode)
            {
                case NameplateMode.Default:
                case NameplateMode.Hide:
                    break;

                case NameplateMode.SmallJobIcon:
                    var nameString = GetStateNametext(iconID, "");
                    var originalName = SeStringUtils.SeStringFromPtr(name);
                    nameString.Append(originalName);

                    name = SeStringUtils.SeStringToPtr(nameString);
                    iconID = GetClassIcon(npObject.NamePlateInfo);
                    break;

                case NameplateMode.SmallJobIconAndRole:
                    nameString = new SeString();
                    if (hasRole)
                    {
                        nameString.Append(_stylesheet.GetRolePlate(roleId));
                        nameString.Append(" ");
                    }

                    originalName = SeStringUtils.SeStringFromPtr(name);
                    nameString.Append(originalName);

                    name = SeStringUtils.SeStringToPtr(nameString);
                    iconID = GetClassIcon(npObject.NamePlateInfo);
                    break;

                case NameplateMode.BigJobIcon:
                    name = SeStringUtils.SeStringToPtr(GetStateNametext(iconID, "   "));
                    fcName = SeStringUtils.emptyPtr;
                    displayTitle = false;
                    iconID = GetClassIcon(npObject.NamePlateInfo);
                    break;

                case NameplateMode.BigJobIconAndPartySlot:
                    fcName = SeStringUtils.emptyPtr;
                    displayTitle = false;
                    var partySlot = _partyListHudView.GetPartySlotIndex(npObject.NamePlateInfo.Data.ObjectID.ObjectID) + 1;
                    if (partySlot != null)
                    {
                        var genericRole = JobExtensions.GetRole((Job)npObject.NamePlateInfo.GetJobID());
                        var str = _stylesheet.GetPartySlotNumber(partySlot.Value, genericRole);
                        str.Payloads.Insert(0, new TextPayload("   "));
                        name = SeStringUtils.SeStringToPtr(str);
                        iconID = GetClassIcon(npObject.NamePlateInfo);
                    }
                    else
                    {
                        name = SeStringUtils.emptyPtr;
                        iconID = GetClassIcon(npObject.NamePlateInfo);
                    }
                    break;

                case NameplateMode.RoleLetters:
                    if (hasRole)
                    {
                        name = SeStringUtils.SeStringToPtr(_stylesheet.GetRolePlate(roleId));
                    }
                    else
                    {
                        var genericRole = JobExtensions.GetRole((Job)npObject.NamePlateInfo.GetJobID());
                        name = SeStringUtils.SeStringToPtr(_stylesheet.GetGenericRolePlate(genericRole));
                    }

                    fcName = SeStringUtils.emptyPtr;
                    displayTitle = false;
                    break;
            }
        }

        private int GetClassIcon(XivApi.SafeNamePlateInfo info)
        {
            var genericRole = JobExtensions.GetRole((Job)info.GetJobID());
            var iconSet = _stylesheet.GetGenericRoleIconset(genericRole);
            return _iconSet.GetJobIcon(iconSet, info.GetJobID());
        }

        private SeString GetStateNametext(int iconId, string prefix)
        {
            return iconId switch
            {
                //061521 - party leader
                //061522 - party member

                061523 => SeStringUtils.Icon(BitmapFontIcon.NewAdventurer, prefix),
                061540 => SeStringUtils.Icon(BitmapFontIcon.Mentor, prefix),
                061542 => SeStringUtils.Icon(BitmapFontIcon.MentorPvE, prefix),
                061543 => SeStringUtils.Icon(BitmapFontIcon.MentorCrafting, prefix),
                061544 => SeStringUtils.Icon(BitmapFontIcon.MentorPvP, prefix),
                061547 => SeStringUtils.Icon(BitmapFontIcon.Returner, prefix),
                _      => SeStringUtils.Text(prefix + " ")
            };
        }

        private NameplateMode GetModeForNameplate(XivApi.SafeNamePlateObject npObject)
        {
            var uid = npObject.NamePlateInfo.Data.ObjectID.ObjectID;
            var mode = OthersMode;
            if (_configuration.TestingMode || npObject.NamePlateInfo.IsPartyMember() || uid == ClientState.LocalPlayer?.ObjectId)
            {
                return PartyMode;
            }
            else
            {
                return OthersMode;
            }
        }
    }
}