using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Rewired;
using Rewired.Data;
using Rewired.Data.Mapping;
using RoR2;

namespace SamusMod.Modules
{
    internal static class ExtraInputs
    {
        internal static void AddActionsToInputCatalog()
        {
            InputCatalog.actionToToken[RewiredAction.autoFire] = RewiredAction.autoFire.DisplayToken;
        }

        internal static void AddCustomActions(Action<UserData> orig, UserData self)
        {
            if (self.actions != null)
            {
                var existing = self.actions.FirstOrDefault(action => action.id == RewiredAction.autoFire.ActionId || action.name == RewiredAction.autoFire.Name);
                if (existing == null)
                {
                    self.actions.Add(RewiredAction.autoFire);
                }
                else if (existing.name != RewiredAction.autoFire.Name)
                {
                    SamusPlugin.logger.LogWarning($"Rewired action id {existing.id} is already used by '{existing.name}', the autofire toggle will not work.");
                }
            }

            FillActionMaps(RewiredAction.autoFire, self.keyboardMaps, self.joystickMaps);

            orig(self);
        }

        internal static void OnLoadUserProfiles(On.RoR2.SaveSystem.orig_LoadUserProfiles orig, SaveSystem self)
        {
            orig(self);

            foreach (var (name, userProfile) in self.loadedUserProfiles)
            {
                try
                {
                    AddMissingBindings(userProfile);
                    userProfile.RequestEventualSave();
                }
                catch(Exception e)
                {
                    SamusPlugin.logger.LogWarning($"Failed to add default bindings to '{name}' profile");
                    SamusPlugin.logger.LogError(e);
                }
            }
        }
internal static void OnLoadDefaultProfile(On.RoR2.UserProfile.orig_LoadDefaultProfile orig)
        {
            orig();

            try
            {
                AddMissingBindings(UserProfile.defaultProfile);
            }
            catch(Exception e)
            {
                SamusPlugin.logger.LogWarning($"Failed to add default bindings to default profile");
                SamusPlugin.logger.LogError(e);
            }
        }

        internal static void OnFillDefaultJoystickMaps(On.RoR2.UserProfile.orig_FillDefaultJoystickMaps orig, UserProfile self)
        {
            orig(self);
            AddMissingBindings(self);
        }

        private static void AddMissingBindings(UserProfile userProfile)
        {
            AddActionMaps(RewiredAction.autoFire, userProfile);
        }
        private static void FillActionMaps(RewiredAction action, List<ControllerMap_Editor> keyboardMaps, List<ControllerMap_Editor> joystickMaps)
        {
            if (joystickMaps != null)
            {
                foreach (var joystickMap in joystickMaps)
                {
                    if (joystickMap.categoryId == 0 && joystickMap.actionElementMaps.All(map => map.actionId != action.ActionId))
                    {
                        joystickMap.actionElementMaps.Add(action.DefaultJoystickMap);
                    }
                }
            }
            if (keyboardMaps != null)
            {
                foreach (var keyboardMap in keyboardMaps)
                {
                    if (keyboardMap.categoryId == 0 && keyboardMap.actionElementMaps.All(map => map.actionId != action.ActionId))
                    {
                        keyboardMap.actionElementMaps.Add(action.DefaultKeyboardMap);
                    }
                }
            }
        }
        private static void AddActionMaps(RewiredAction action,UserProfile userProfile)
        {
            foreach (var (_, joystickMap) in userProfile.HardwareJoystickMaps2)
            {
                if (joystickMap.AllMaps.All(map => map.actionId != action.ActionId))
                {
                    var defaultMap = action.DefaultJoystickMap;
                    joystickMap.CreateElementMap(defaultMap.actionId, defaultMap.axisContribution, defaultMap.elementIdentifierId, defaultMap.elementType, defaultMap.axisRange, defaultMap.invert);
                    LinkToControllerMap(defaultMap, joystickMap);
                }
            }

            if (userProfile.keyboardMap.AllMaps.All(map => map.actionId != action.ActionId))
            {
                var defaultMap = action.DefaultKeyboardMap;
                userProfile.keyboardMap.CreateElementMap(defaultMap.actionId, defaultMap.axisContribution, defaultMap.elementIdentifierId, action.DefaultJoystickMap.elementType, action.DefaultJoystickMap.axisRange, action.DefaultJoystickMap.invert, out var createdMap);
                createdMap._keyboardKeyCode = action.DefaultKeyboardKey;
                LinkToControllerMap(defaultMap, userProfile.keyboardMap);
            }
        }

        // Rewired's internals are obfuscated and renamed every build, so find the method that attaches an
        // ActionElementMap to its ControllerMap by shape: the only non-public ActionElementMap method taking just a ControllerMap.
        private static MethodInfo linkToControllerMap;
        private static void LinkToControllerMap(ActionElementMap elementMap, ControllerMap controllerMap)
        {
            if (linkToControllerMap == null)
            {
                linkToControllerMap = typeof(ActionElementMap)
                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(method => method.ReturnType == typeof(void)
                                           && method.GetParameters().Length == 1
                                           && method.GetParameters()[0].ParameterType == typeof(ControllerMap));
            }

            if (linkToControllerMap == null)
            {
                SamusPlugin.logger.LogWarning("Could not find the Rewired method that links an element map to its controller map.");
                return;
            }

            linkToControllerMap.Invoke(elementMap, new object[] { controllerMap });
        }

        // The method on UserData that wraps its lists in read-only collections is obfuscated and renamed every build.
        // It is the only non-public, parameterless, void instance method on UserData besides SetDefaultValuesOnCreation.
        internal static MethodInfo FindUserDataInitMethod()
        {
            var candidates = typeof(UserData)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.ReturnType == typeof(void)
                              && method.GetParameters().Length == 0
                              && !method.IsSpecialName
                              && method.Name != "SetDefaultValuesOnCreation")
                .ToList();

            return candidates.Count == 1 ? candidates[0] : null;
        }

        private static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
    }

    public class RewiredAction
    {
        public static RewiredAction autoFire { get; }

        public int ActionId{ get; private set; }
        public string Name { get; private set; }
        public string DisplayToken { get; private set; }
        public KeyboardKeyCode DefaultKeyboardKey { get; private set; }
        public int DefaultJoystickKey { get; private set; }

        private InputAction inputAction;

        private ActionElementMap _defaultJoystickMap;
        public ActionElementMap DefaultJoystickMap
        {
            get
            {
               return _defaultJoystickMap ?? (_defaultJoystickMap = new ActionElementMap(ActionId, ControllerElementType.Button, DefaultJoystickKey, Pole.Positive, AxisRange.Positive));
            }
        }

        private ActionElementMap _defaultKeyboardMap;
        public ActionElementMap DefaultKeyboardMap 
        {
            get
            {
                return _defaultKeyboardMap ?? (_defaultKeyboardMap = new ActionElementMap(ActionId, ControllerElementType.Button, (int)DefaultKeyboardKey-21) { _keyboardKeyCode = DefaultKeyboardKey });
            }
        
        
        }

        static RewiredAction()
        {
            autoFire = new RewiredAction
            {
                ActionId = 388,
                Name = "autoFireToggle",
                DisplayToken = "SAMUS_AUTOFIRE_TOGGLE",
                DefaultKeyboardKey = KeyboardKeyCode.G,
                DefaultJoystickKey = 16
            };
        }
        public static implicit operator InputCatalog.ActionAxisPair(RewiredAction action)
        {
            return new InputCatalog.ActionAxisPair(action.Name, AxisRange.Full);
        }
        public static implicit operator InputAction(RewiredAction action)
        {
            return action.inputAction ?? new InputAction
            {
                id = action.ActionId,
                name = action.Name,
                type = InputActionType.Button,
                descriptiveName = action.Name,
                behaviorId = 0,
                userAssignable = true,
                categoryId = 0
            };
        } 
    }
}
