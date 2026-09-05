// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Battle;
using Ficedula.FF7;
using Microsoft.Xna.Framework;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.UI.Layout {
    public class Status : LayoutModel {

        public override bool IsRazorModel => true;
        public override string Description => "Status";

        public Group gPortrait, gSummary, gSummaryRight, gContent;

        public Character Character => _game.SaveData.Party[(int)_screen.Param];
        public Weapon Weapon => Character.GetWeapon(_game);
        public Armour Armour => Character.GetArmour(_game);
        public Accessory Accessory => Character.GetAccessory(_game);
        public CombatStats CombatStats => Character.GetBaseCombatStats(_game);

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

        public override bool ProcessInput(InputState input)
        {

            if (input.IsJustDown(InputKey.Cancel))
            {
                _screen.FadeOut(() => _game.PopScreen(_screen));
                return true;
            }

            return false;
        }
    }
}
