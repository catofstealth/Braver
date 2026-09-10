// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.ComponentModel.Design.ObjectSelectorEditor;

namespace Braver.UI.Layout {
    public class EquipMenu : LayoutModel {

        public Label lWeapon, lArmour, lAccessory,
            lWeaponText, lArmourText, lAccessoryText,
            lDescription, lMateriaGrowth,
            lAttackFrom, lAttackPCFrom, lDefenseFrom, lDefensePCFrom, lMAttackFrom, lMDefFrom, lMDefPCFrom,
            lAttackTo, lAttackPCTo, lDefenseTo, lDefensePCTo, lMAttackTo, lMDefTo, lMDefPCTo;

        public MateriaDisplay lItemMateriaSlotsDisplay;
        public Group gMenu, gStats, gMateriaSlots;
        public List lbWeapons, lbArmour, lbAccessories;

        public override bool IsRazorModel => true;

        public Character Character => _game.SaveData.Party[(int)_screen.Param];
        public Weapon Weapon => Character.GetWeapon(_game);
        public Armour Armour => Character.GetArmour(_game);
        public Accessory Accessory => Character.GetAccessory(_game);

        public MateriaItem MateriaItem;
        
        private Weapon _focusedWeapon;
        private Armour _focusedArmour;
        private Accessory _focusedAccessory;

        public Weapon FocusedWeapon => FocusGroup == lbWeapons ? _focusedWeapon : null;
        public Armour FocusedArmour => FocusGroup == lbArmour ? _focusedArmour : null;
        public Accessory FocusedAccessory => FocusGroup == lbAccessories ? _focusedAccessory : null;

        public List<Weapon> CharacterAvailableWeapons => AvailableWeapons.Where(x => ((x.EquippableOn >> Character.CharIndex) & 0x1) == 1).ToList();
        public List<Armour> CharacterAvailableArmour => AvailableArmour.Where(x => ((x.EquippableOn >> Character.CharIndex) & 0x1) == 1).ToList();
        public List<Accessory> CharacterAvailableAccessories => AvailableAccessories.Where(x => ((x.EquippableOn >> Character.CharIndex) & 0x1) == 1).ToList();
        public List<Weapon> AvailableWeapons { get; } = new();
        public List<Armour> AvailableArmour { get; } = new();
        public List<Accessory> AvailableAccessories { get; } = new();

        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);

            var kernel = _game.Singleton(() => new Kernel(_game.Open("kernel", "kernel.bin")));
            var weapons = _game.Singleton(() => new WeaponCollection(kernel));
            var armours = _game.Singleton(() => new ArmourCollection(kernel));
            var accessories = _game.Singleton(() => new AccessoryCollection(kernel));

            AvailableWeapons.Clear();
            AvailableWeapons.AddRange(
                g.SaveData.Inventory
                .Where(inv => (inv.ItemID >= InventoryItem.ITEM_ID_CUTOFF) && (inv.ItemID < InventoryItem.WEAPON_ID_CUTOFF))
                .Select(inv => inv.ItemID)
                .Concat(new[] { Character.EquipWeapon + InventoryItem.ITEM_ID_CUTOFF })
                .Where(id => id >= 0)
                .Distinct()
                .OrderBy(i => i)
                .Select(i => weapons.Weapons[i - InventoryItem.ITEM_ID_CUTOFF])
            );

            AvailableArmour.Clear();
            AvailableArmour.AddRange(
                g.SaveData.Inventory
                .Where(inv => (inv.ItemID >= InventoryItem.WEAPON_ID_CUTOFF) && (inv.ItemID < InventoryItem.ARMOUR_ID_CUTOFF))
                .Select(inv => inv.ItemID)
                .Concat(new[] { Character.EquipArmour + InventoryItem.WEAPON_ID_CUTOFF })
                .Where(id => id >= 0)
                .Distinct()
                .OrderBy(i => i)
                .Select(i => armours.Armour[i - InventoryItem.WEAPON_ID_CUTOFF])
            );

            AvailableAccessories.Clear();
            AvailableAccessories.AddRange(
                g.SaveData.Inventory
                .Where(inv => (inv.ItemID >= InventoryItem.ARMOUR_ID_CUTOFF) && (inv.ItemID < InventoryItem.ACCESSORY_ID_CUTOFF))
                .Select(inv => inv.ItemID)
                .Concat(Character.EquipAccessory >= 0 ? new [] { Character.EquipAccessory + InventoryItem.ARMOUR_ID_CUTOFF } : [])
                .Where(id => id >= 0)
                .Distinct()
                .OrderBy(i => i)
                .Select(i => accessories.Accessories[i - InventoryItem.ARMOUR_ID_CUTOFF])
            );

            MateriaItem = Weapon;
        }

        protected override void OnInit() {
            base.OnInit();
            if (Focus == null) {
                PushFocus(gMenu, lWeapon);
            }
            ResetAllLabels();
        }

        public override void CancelPressed() {
            if (FocusGroup == gMenu)
            {
                _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
                InputEnabled = false;
                _screen.FadeOut(() => _game.PopScreen(_screen));
            }
            else
            {
                ResetAllLabels();
                base.CancelPressed();
            }
        }

        private void UpdateMateriaItem(MateriaItem item = null)
        {
            MateriaItem = item;

            if (MateriaItem == null)
            {
                gMateriaSlots.Visible = false;
                return;
            }

            gMateriaSlots.Visible = true;
            
            lItemMateriaSlotsDisplay.MateriaSlots = MateriaItem.MateriaSlots;
            lItemMateriaSlotsDisplay.ZeroGrowthRate = MateriaItem.MateriaGrowthRate == 0;

            lMateriaGrowth.Text = MateriaItem.MateriaGrowthRate switch {
                0 => "Nothing",
                2 => "Double",
                3 => "Triple",
                _ => "Normal"
            }; 
        }

        public void MenuSelected(Label selected) {
            if (selected == lWeapon) {
                if (AvailableWeapons.Any()) {
                    PushFocus(lbWeapons, lbWeapons.Children[0]);
                } else
                    _game.Audio.PlaySfx(Sfx.Invalid, 1f, 0f);
            } else if (selected == lArmour) {
                if (AvailableArmour.Any()) {
                    PushFocus(lbArmour, lbArmour.Children[0]);
                } else
                    _game.Audio.PlaySfx(Sfx.Invalid, 1f, 0f);
            } else if (selected == lAccessory) {
                if (AvailableAccessories.Any()) {
                    PushFocus(lbAccessories, lbAccessories.Children[0]);
                } else
                    _game.Audio.PlaySfx(Sfx.Invalid, 1f, 0f);
            }
            UpdateControls();
        }

        private void UpdateControls() {
            lbWeapons.Visible = FocusGroup == lbWeapons;
            lbArmour.Visible = FocusGroup == lbArmour;          
            lbAccessories.Visible = FocusGroup == lbAccessories;
        }

        private void SetLabels(int oldvalue, int? newvalue, Label lOld, Label lNew) {
            lOld.Text = oldvalue.ToString();
            if (newvalue == null || newvalue.Value == oldvalue) {
                lNew.Color = Color.White;
                lNew.Text = newvalue.ToString();
            } else {
                lNew.Color = newvalue.Value < oldvalue ? Color.Red : Color.Yellow;
                lNew.Text = newvalue.Value.ToString();
            }
        }

        private void ResetAllLabels(bool populateBaseStats = false)
        {
            if (_game.GameOptions.DisplayLegacyStats)
            {
                SetLabels(Weapon.AttackStrength, populateBaseStats ? Weapon.AttackStrength : null, lAttackFrom, lAttackTo);
                SetLabels(Weapon.HitChance, populateBaseStats ? Weapon.HitChance : null, lAttackPCFrom, lAttackPCTo);
                SetLabels(Armour.Defense, populateBaseStats ? Armour.Defense : null, lDefenseFrom, lDefenseTo);
                SetLabels(Armour.DefensePercent, populateBaseStats ? Armour.DefensePercent : null, lDefensePCFrom, lDefensePCTo);
                SetLabels(Weapon.SprBonus + Armour.SprBonus, populateBaseStats ? Weapon.SprBonus + Armour.SprBonus : null, lMAttackFrom, lMAttackTo);
                SetLabels(Armour.MDefense, populateBaseStats ? Armour.MDefense : null, lMDefFrom, lMDefTo);
                SetLabels(Armour.MDefensePercent, populateBaseStats ? Armour.MDefensePercent : null, lMDefPCFrom, lMDefPCTo);
            }
            else
            {
                SetLabels(Character.Strength + Weapon.AttackStrength, populateBaseStats ? Character.Strength + Weapon.AttackStrength : null, lAttackFrom, lAttackTo);
                SetLabels(Weapon.HitChance, populateBaseStats ? Weapon.HitChance : null, lAttackPCFrom, lAttackPCTo);
                SetLabels(Character.Vitality + Armour.Defense, populateBaseStats ? Character.Vitality + Armour.Defense : null, lDefenseFrom, lDefenseTo);
                SetLabels(Character.Dexterity / 4 + Armour.DefensePercent, populateBaseStats ? Character.Dexterity / 4 + Armour.DefensePercent : null, lDefensePCFrom, lDefensePCTo);
                SetLabels(Weapon.SprBonus + Armour.SprBonus, populateBaseStats ? Weapon.SprBonus + Armour.SprBonus : null, lMAttackFrom, lMAttackTo);
                SetLabels(Character.Spirit + Armour.MDefense, populateBaseStats ? Character.Spirit + Armour.MDefense : null, lMDefFrom, lMDefTo);
                SetLabels(Armour.MDefensePercent, populateBaseStats ? Armour.MDefensePercent : null, lMDefPCFrom, lMDefPCTo);
            }
        }

        public void WeaponFocussed() {
            _focusedWeapon = CharacterAvailableWeapons[lbWeapons.GetSelectedIndex(this)];
            lDescription.Text = _focusedWeapon.Description;
            ResetAllLabels(true);
            if (_game.GameOptions.DisplayLegacyStats)
            {
                SetLabels(Weapon.AttackStrength, _focusedWeapon.AttackStrength, lAttackFrom, lAttackTo);
                SetLabels(Weapon.HitChance, _focusedWeapon.HitChance, lAttackPCFrom, lAttackPCTo);
            }
            else
            {
                SetLabels(Character.Strength + Weapon.AttackStrength, Character.Strength + _focusedWeapon.AttackStrength, lAttackFrom, lAttackTo);
                SetLabels(Weapon.HitChance, _focusedWeapon.HitChance, lAttackPCFrom, lAttackPCTo);
            }
            UpdateMateriaItem(_focusedWeapon);
        }

        public void ArmourFocussed() {
            _focusedArmour = CharacterAvailableArmour[lbArmour.GetSelectedIndex(this)];
            lDescription.Text = _focusedArmour.Description;
            ResetAllLabels(true);
            if (_game.GameOptions.DisplayLegacyStats)
            {
                SetLabels(Armour.Defense, _focusedArmour.Defense, lDefenseFrom, lDefenseTo);
                SetLabels(Armour.DefensePercent, _focusedArmour.DefensePercent, lDefensePCFrom, lDefensePCTo);
                SetLabels(Armour.MDefense, _focusedArmour.MDefense, lMDefFrom, lMDefTo);
                SetLabels(Armour.MDefensePercent, _focusedArmour.MDefensePercent, lMDefPCFrom, lMDefPCTo);
            }
            else
            {
                SetLabels(Character.Vitality + Armour.Defense, Character.Vitality + _focusedArmour.Defense, lDefenseFrom, lDefenseTo);
                SetLabels(Character.Dexterity / 4 + Armour.DefensePercent, Character.Dexterity / 4 + _focusedArmour.DefensePercent, lDefensePCFrom, lDefensePCTo);
                SetLabels(Character.Spirit + Armour.MDefense, Character.Spirit + _focusedArmour.MDefense, lMDefFrom, lMDefTo);
                SetLabels(Armour.MDefensePercent, _focusedArmour.MDefensePercent, lMDefFrom, lMDefTo);
            }
            UpdateMateriaItem(_focusedArmour);
        }

        public void AccessoryFocussed() {
            _focusedAccessory = CharacterAvailableAccessories[lbAccessories.GetSelectedIndex(this)];            
            lDescription.Text = _focusedAccessory.Description;
            ResetAllLabels(true);
            if (_game.GameOptions.DisplayLegacyStats)
            {
                //TODO - Str/Vit/Spr bonuses would affect things!
            }
            else
            { 
            }
            MateriaItem = null;
            UpdateMateriaItem();
        }

        public void WeaponMenuFocussed()
        {
            lDescription.Text = Weapon.Description;
            ResetAllLabels(true);
            UpdateMateriaItem(Weapon);
        }

        public void ArmourMenuFocussed()
        {
            lDescription.Text = Armour.Description;
            ResetAllLabels(true);
            UpdateMateriaItem(Armour);
        }

        public void AccessoryMenuFocussed()
        {
            lDescription.Text = Accessory?.Description ?? String.Empty;
            ResetAllLabels(true);
            UpdateMateriaItem();
        }

        public void WeaponSelected(Label L) {
            PopFocus();
            int id = int.Parse(L.ID.Substring(6));
            if (id != Character.EquipWeapon) {
                _game.SaveData.GiveInventoryItem(Character.EquipWeapon + InventoryItem.ITEM_ID_CUTOFF);
                _game.SaveData.TakeInventoryItem(id + InventoryItem.ITEM_ID_CUTOFF);
                Character.EquipWeapon = id;
                Character.Recalculate(_game);
                _screen.Reload();
            }
            lDescription.Text = Weapon.Description;
            UpdateMateriaItem(Weapon);
        }

        public void ArmourSelected(Label L) {
            PopFocus();
            int id = int.Parse(L.ID.Substring(6));
            if (id != Character.EquipArmour) {
                _game.SaveData.GiveInventoryItem(Character.EquipArmour + InventoryItem.WEAPON_ID_CUTOFF);
                _game.SaveData.TakeInventoryItem(id + InventoryItem.WEAPON_ID_CUTOFF);
                Character.EquipArmour = id;
                Character.Recalculate(_game);
                _screen.Reload();
            }
            lDescription.Text = Armour.Description;
            UpdateMateriaItem(Armour);
        }

        public void AccessorySelected(Label L) {
            PopFocus();
            int id = int.Parse(L.ID.Substring(9));
            if (id != Character.EquipAccessory) {
                if(Character.EquipAccessory >= 0)
                {
                    _game.SaveData.GiveInventoryItem(Character.EquipAccessory + InventoryItem.ARMOUR_ID_CUTOFF);
                }
                _game.SaveData.TakeInventoryItem(id + InventoryItem.ARMOUR_ID_CUTOFF);
                Character.EquipAccessory = id;
                Character.Recalculate(_game);
                _screen.Reload();
            }
            lDescription.Text = Accessory?.Description ?? String.Empty;
            UpdateMateriaItem();
        }

        public override bool ProcessInput(InputState input) {
            int charIndex = (int)_screen.Param;
            if (input.IsJustDown(InputKey.PanLeft)) {
                charIndex = (charIndex + _game.SaveData.Party.Length - 1) % _game.SaveData.Party.Length;
                _game.PopScreen(_screen);
                _game.PushScreen(new LayoutScreen("EquipMenu", parm: charIndex));
                return true;
            } else if (input.IsJustDown(InputKey.PanRight)) {
                charIndex = (charIndex + _game.SaveData.Party.Length - 1) % _game.SaveData.Party.Length;
                _game.PopScreen(_screen);
                _game.PushScreen(new LayoutScreen("EquipMenu", parm: charIndex));
                return true;
            } else if (input.IsJustDown(InputKey.Menu))
            {
                //remove accessory
                if (_focusedAccessory != null && Character.EquipAccessory >= 0)
                {
                    _game.SaveData.GiveInventoryItem(Character.EquipAccessory + InventoryItem.ARMOUR_ID_CUTOFF);
                    Character.EquipAccessory = -1;
                    Character.Recalculate(_game);
                    _screen.Reload();
                }
                return true;
            } else 
                return base.ProcessInput(input); 
        }
    }
}
