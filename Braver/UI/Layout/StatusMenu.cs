// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Battle;
using Ficedula.FF7;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Braver.UI.Layout {
    public class StatusMenu : LayoutModel {

        public override bool IsRazorModel => true;
        public override string Description => "Status";

        public Group gPortrait, gSummary, gSummaryRight, gContent, gElementContent, gEffectContent;
        public Group gAttackElement, gHalveElement, gInvalidElement, gAbsorbElement;
        public Group gAttackEffect, gDefendEffect;

        public Character Character => _game.SaveData.Party[(int)_screen.Param];
        public Weapon Weapon => Character.GetWeapon(_game);
        public Armour Armour => Character.GetArmour(_game);

        private Materias _materias => _game.Singleton<Materias>();

        public IReadOnlyList<AvailableMateria> WeaponMateria
        {
            get => Character.WeaponMateria
                .Select(m => m == null ? null : new UI.Layout.AvailableMateria
                {
                    AP = m.AP,
                    Materia = _materias[m.MateriaID],
                })
                .ToList();
        }
        public IReadOnlyList<AvailableMateria> ArmourMateria
        {
            get => Character.ArmourMateria
                .Select(m => m == null ? null : new UI.Layout.AvailableMateria
                {
                    AP = m.AP,
                    Materia = _materias[m.MateriaID],
                })
                .ToList();
        }

        public Accessory Accessory => Character.GetAccessory(_game);
        public CombatStats CombatStats => Character.GetBaseCombatStats(_game);

        private StatusScreen currentScreen = StatusScreen.Overview;
        private enum StatusScreen { 
            Overview,
            Element,
            Effect
        }

        public override void Created(FGame g, LayoutScreen screen)
        {
            base.Created(g, screen);
            Character.Recalculate(_game);
        }

        protected override void OnInit() {
            base.OnInit();
            Update();
        }

        private void Update() {

        }

        public void LabelClick(Label L) {

        }

        private void UpdateElementScreen()
        {
            foreach (var element in Weapon.GetElements())
            {
                var label = (Label)gAbsorbElement.Children.SingleOrDefault(x => x.ID == "l" + element.ToString());
                if (label != null)
                {
                    label.Color = Color.White;
                }
            }

            for (int m = 0; m < Weapon.MateriaSlots.Count; m++)
            {
                if (m == Weapon.MateriaSlots.Count - 1)
                {
                    //last element in the chain, nothing to do 
                    continue;
                }

                //make sure slots are linked
                if (Weapon.MateriaSlots[m] != MateriaSlotKind.Linked
                    && Weapon.MateriaSlots[m + 1] != MateriaSlotKind.Linked)
                {
                    continue;
                }

                //make sure slots aren't empty
                if (Character.WeaponMateria[m] == null
                    || Character.WeaponMateria[m + 1] == null)
                {
                    continue;
                }

                var materia = _materias[Character.WeaponMateria[m].MateriaID];
                var materia2 = _materias[Character.WeaponMateria[m + 1].MateriaID];

                if (materia == null || materia2 == null)
                {
                    continue;
                }

                if (materia.GetType() == typeof(SupportMateria)
                    && ((SupportMateria)materia).Kind == SupportMateriaKind.Elemental
                    && materia2.Element != Element.None)
                {
                    var label = (Label)gAttackElement.Children.SingleOrDefault(x => x.ID == "l" + materia2.Element.ToString());
                    if (label != null)
                    {
                        label.Color = Color.White;
                    }
                }
                else if (materia2.GetType() == typeof(SupportMateria)
                    && ((SupportMateria)materia2).Kind == SupportMateriaKind.Elemental
                    && materia.Element != Element.None)
                {
                    var label = (Label)gAttackElement.Children.SingleOrDefault(x => x.ID == "l" + materia.Element.ToString());
                    if (label != null)
                    {
                        label.Color = Color.White;
                    }
                }
            }

            for (int m = 0; m < Armour.MateriaSlots.Count; m++)
            {
                if (m == Armour.MateriaSlots.Count - 1)
                {
                    //last element in the chain, nothing to do 
                    continue;
                }

                //make sure slots are linked
                if (Armour.MateriaSlots[m] != MateriaSlotKind.Linked
                    && Armour.MateriaSlots[m + 1] != MateriaSlotKind.Linked)
                {
                    continue;
                }

                //make sure slots aren't empty
                if (Character.ArmourMateria[m] == null
                    || Character.ArmourMateria[m + 1] == null)
                {
                    continue;
                }

                var materia = _materias[Character.ArmourMateria[m].MateriaID];
                var materia2 = _materias[Character.ArmourMateria[m + 1].MateriaID];

                if (materia == null || materia2 == null)
                {
                    continue;
                }

                if (materia.GetType() == typeof(SupportMateria)
                    && ((SupportMateria)materia).Kind == SupportMateriaKind.Elemental
                    && materia2.Element != Element.None)
                {
                    var label = (Label)gAbsorbElement.Children.SingleOrDefault(x => x.ID == "l" + materia2.Element.ToString());
                    if (label != null)
                    {
                        label.Color = Color.White;
                    }
                }
                else if (materia2.GetType() == typeof(SupportMateria)
                    && ((SupportMateria)materia2).Kind == SupportMateriaKind.Elemental
                    && materia.Element != Element.None)
                {
                    var label = (Label)gAbsorbElement.Children.SingleOrDefault(x => x.ID == "l" + materia.Element.ToString());
                    if (label != null)
                    {
                        label.Color = Color.White;
                    }
                }
            }

            switch (Armour.ElementEffect)
            {
                case EquipElement.Absorb:
                    foreach (var element in Armour.GetElements())
                    {
                        var label = (Label)gAbsorbElement.Children.SingleOrDefault(x => x.ID == "l" + element.ToString());
                        if (label != null)
                        {
                            label.Color = Color.White;
                        }
                    }
                    break;
                case EquipElement.Nullify:
                    foreach (var element in Armour.GetElements())
                    {
                        var label = (Label)gInvalidElement.Children.SingleOrDefault(x => x.ID == "l" + element.ToString());
                        if (label != null)
                        {
                            label.Color = Color.White;
                        }
                    }
                    break;
                case EquipElement.Halve:
                    foreach (var element in Armour.GetElements())
                    {
                        var label = (Label)gHalveElement.Children.SingleOrDefault(x => x.ID == "l" + element.ToString());
                        if (label != null)
                        {
                            label.Color = Color.White;
                        }
                    }
                    break;
            }

            if (Accessory != null)
            {
                switch (Accessory.ElementEffect)
                {
                    case EquipElement.Absorb:
                        foreach (var element in Accessory.GetElements())
                        {
                            var label = (Label)gAbsorbElement.Children.SingleOrDefault(x => x.ID == "l" + element.ToString());
                            if (label != null)
                            {
                                label.Color = Color.White;
                            }
                        }
                        break;
                    case EquipElement.Nullify:
                        foreach (var element in Accessory.GetElements())
                        {
                            var label = (Label)gInvalidElement.Children.SingleOrDefault(x => x.ID == "l" + element.ToString());
                            if (label != null)
                            {
                                label.Color = Color.White;
                            }
                        }
                        break;
                    case EquipElement.Halve:
                        foreach (var element in Accessory.GetElements())
                        {
                            var label = (Label)gHalveElement.Children.SingleOrDefault(x => x.ID == "l" + element.ToString());
                            if (label != null)
                            {
                                label.Color = Color.White;
                            }
                        }
                        break;
                }
            }
        }

        private void UpdateEffectScreen()
        {
            //TODO : weapon status does not appear to be set but could be used for modding

            foreach (var status in Armour.GetStatusDefenses())
            {
                var label = (Label)gDefendEffect.Children.SingleOrDefault(x => x.ID == "l" + status.ToString());
                if (label != null)
                {
                    label.Color = Color.White;
                }
            }

            if (Accessory != null)
            {
                foreach (var status in Accessory.GetStatusDefenses())
                {
                    var label = (Label)gDefendEffect.Children.SingleOrDefault(x => x.ID == "l" + status.ToString());
                    if (label != null)
                    {
                        label.Color = Color.White;
                    }
                }
            }

            for (int m = 0; m < Weapon.MateriaSlots.Count; m++)
            {
                if (m == Weapon.MateriaSlots.Count - 1)
                {
                    //last element in the chain, nothing to do 
                    continue;
                }
                //make sure slots are linked
                if (Weapon.MateriaSlots[m] != MateriaSlotKind.Linked
                    && Weapon.MateriaSlots[m + 1] != MateriaSlotKind.Linked)
                {
                    continue;
                }
                //make sure slots aren't empty
                if (Character.WeaponMateria[m] == null
                    || Character.WeaponMateria[m + 1] == null)
                {
                    continue;
                }

                var materia = _materias[Character.WeaponMateria[m].MateriaID];
                var materia2 = _materias[Character.WeaponMateria[m + 1].MateriaID];

                if (materia == null || materia2 == null)
                {
                    continue;
                }

                if (materia.GetType() == typeof(SupportMateria)
                    && ((SupportMateria)materia).Kind == SupportMateriaKind.AddedEffect
                    && materia2.Statuses != Statuses.None)
                {
                    foreach (var status in materia2.GetStatusDefenses())
                    {
                        var label = (Label)gAttackEffect.Children.SingleOrDefault(x => x.ID == "l" + status.ToString());
                        if (label != null)
                        {
                            label.Color = Color.White;
                        }
                    }
                }
                else if (materia2.GetType() == typeof(SupportMateria)
                    && ((SupportMateria)materia2).Kind == SupportMateriaKind.AddedEffect
                    && materia.Statuses != Statuses.None)
                {
                    foreach (var status in materia.GetStatusDefenses())
                    {
                        var label = (Label)gAttackEffect.Children.SingleOrDefault(x => x.ID == "l" + status.ToString());
                        if (label != null)
                        {
                            label.Color = Color.White;
                        }
                    }
                }

                m++;
            }

            for (int m = 0; m < Armour.MateriaSlots.Count; m++)
            {
                if (m == Armour.MateriaSlots.Count - 1)
                {
                    //last element in the chain, nothing to do 
                    continue;
                }
                //make sure slots are linked
                if (Armour.MateriaSlots[m] != MateriaSlotKind.Linked
                    && Armour.MateriaSlots[m + 1] != MateriaSlotKind.Linked)
                {
                    continue;
                }
                //make sure slots aren't empty
                if (Character.ArmourMateria[m] == null
                    || Character.ArmourMateria[m + 1] == null)
                {
                    continue;
                }

                var materia = _materias[Character.ArmourMateria[m].MateriaID];
                var materia2 = _materias[Character.ArmourMateria[m + 1].MateriaID];

                if (materia == null || materia2 == null)
                {
                    continue;
                }

                if (materia.GetType() == typeof(SupportMateria)
                    && ((SupportMateria)materia).Kind == SupportMateriaKind.AddedEffect
                    && materia2.Statuses != Statuses.None)
                {
                    //loop through statuses
                    foreach (var status in materia2.GetStatusDefenses())
                    {
                        var label = (Label)gDefendEffect.Children.SingleOrDefault(x => x.ID == "l" + status.ToString());
                        if (label != null)
                        {
                            label.Color = Color.White;
                        }
                    }
                }
                else if (materia2.GetType() == typeof(SupportMateria)
                    && ((SupportMateria)materia2).Kind == SupportMateriaKind.AddedEffect
                    && materia.Statuses != Statuses.None)
                {
                    foreach (var status in materia.GetStatusDefenses())
                    {
                        var label = (Label)gDefendEffect.Children.SingleOrDefault(x => x.ID == "l" + status.ToString());
                        if (label != null)
                        {
                            label.Color = Color.White;
                        }
                    }
                }

                m++;
            }
        }

        public string MateriaColor(Materia m)
        {
            switch (m)
            {
                case MagicMateria:
                    return "magic";
                case CommandMateria:
                    return "command";
                case IndependentMateria:
                    return "independent";
                case SupportMateria:
                    return "support";
                case SummonMateria:
                default:
                    return "summon";
            }
        }

        public override bool ProcessInput(InputState input)
        {
            //flip through screens for same char
            if (input.IsJustDown(InputKey.OK))
            {
                switch (currentScreen)
                {
                    case StatusScreen.Overview:
                        gContent.Visible = false;
                        gElementContent.Visible = true;
                        gEffectContent.Visible = false;
                        currentScreen = StatusScreen.Element;
                        UpdateElementScreen();
                        break;
                    case StatusScreen.Element:
                        gContent.Visible = false;
                        gElementContent.Visible = false;
                        gEffectContent.Visible = true;
                        currentScreen = StatusScreen.Effect;
                        UpdateEffectScreen();
                        break;
                    case StatusScreen.Effect:
                        gContent.Visible = true;
                        gElementContent.Visible = false;
                        gEffectContent.Visible = false;
                        currentScreen = StatusScreen.Overview;
                        break;
                }
            }

            //flip through party chars
            int charIndex = (int)_screen.Param;
            if (input.IsJustDown(InputKey.PanLeft))
            {
                charIndex = (charIndex - _game.SaveData.Party.Length - 1) % _game.SaveData.Party.Length;
                _game.PopScreen(_screen);
                _game.PushScreen(new LayoutScreen("Status", parm: charIndex));
                return true;
            }

            if (input.IsJustDown(InputKey.PanRight))
            {
                charIndex = (charIndex + _game.SaveData.Party.Length - 1) % _game.SaveData.Party.Length;
                _game.PopScreen(_screen);
                _game.PushScreen(new LayoutScreen("Status", parm: charIndex));
                return true;
            }

            if (input.IsJustDown(InputKey.Cancel))
            {
                _screen.FadeOut(() => _game.PopScreen(_screen));
                return true;
            }

            return false;
        }
    }
}
