// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ficedula.FF7
{

    [Flags]
    public enum TargettingFlags
    {
        None = 0,
        EnableSelection = 0x1,
        StartOnEnemy = 0x2,
        MultiTargets = 0x4,
        ToggleMultiSingleTarget = 0x8,
        OneRowOnly = 0x10,
        ShortRange = 0x20,
        AllRows = 0x40,
        RandomTarget = 0x80,
    }

    public class EquipItem
    {
        public int ID { get; set; }
        public EquipRestrictions Restrictions { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public int StrBonus { get; set; }
        public int VitBonus { get; set; }
        public int MagBonus { get; set; }
        public int SprBonus { get; set; }
        public int DexBonus { get; set; }
        public int LckBonus { get; set; }

        public int EquippableOn { get; set; } //TODO - enum flags?

        public void ApplyStatModifiers(StatModifiers statModifiers)
        {
            statModifiers.ApplyModifiers(this);
        }
    }

    public class StatModifiers
    {
        private ushort[] Modifiers = new ushort[4];
        private ushort[] Values = new ushort[4];
        private int ModifierSlots;

        public StatModifiers(uint modifiers, uint values)
        {
            ModifierSlots = 4;

            Modifiers[0] = BitConverter.GetBytes(modifiers)[0];
            Modifiers[1] = BitConverter.GetBytes(modifiers)[1];
            Modifiers[2] = BitConverter.GetBytes(modifiers)[2];
            Modifiers[3] = BitConverter.GetBytes(modifiers)[3];

            Values[0] = BitConverter.GetBytes(values)[0];
            Values[1] = BitConverter.GetBytes(values)[1];
            Values[2] = BitConverter.GetBytes(values)[2];
            Values[3] = BitConverter.GetBytes(values)[3];
        }

        public StatModifiers(ushort modifiers, ushort values)
        {
            ModifierSlots = 2;

            Modifiers[0] = BitConverter.GetBytes(modifiers)[0];
            Modifiers[1] = BitConverter.GetBytes(modifiers)[1];

            Values[0] = BitConverter.GetBytes(values)[0];
            Values[1] = BitConverter.GetBytes(values)[1];
        }

        public void ApplyModifiers(EquipItem equippableItem)
        {
            for (int x = 0; x < ModifierSlots; x++)
            {
                switch (Modifiers[x])
                {
                    case 0:
                        equippableItem.StrBonus = Values[x];
                        break;
                    case 1:
                        equippableItem.VitBonus = Values[x];
                        break;
                    case 2:
                        equippableItem.MagBonus = Values[x];
                        break;
                    case 3:
                        equippableItem.SprBonus = Values[x];
                        break;
                    case 4:
                        equippableItem.DexBonus = Values[x];
                        break;
                    case 5:
                        equippableItem.LckBonus = Values[x];
                        break;
                }
            }
        }
    }

    public class MateriaItem : EquipItem
    {
        public List<MateriaSlotKind> MateriaSlots { get; } = new();
        public int Growth { get; set; }
    }

    public class Weapon : MateriaItem
    {
        public TargettingFlags TargettingFlags { get; set; }
        public byte DamageFormula { get; set; }
        public byte AttackStrength { get; set; }
        public Statuses Statuses { get; set; }
        public int CriticalChance { get; set; }
        public int HitChance { get; set; }
        public byte AnimationModifier { get; set; }
        public byte AttackModel { get; set; }
        public Elements Elements { get; set; }
        public int HitSoundEffect { get; set; }
        public int CriticalSoundEffect { get; set; }
        public int MissSoundEffect { get; set; }
        public byte ImpactEffectID { get; set; }

    }

    public class WeaponCollection
    {
        private List<Weapon> _weapons = new();

        public IReadOnlyList<Weapon> Weapons => _weapons.AsReadOnly();

        public WeaponCollection(Kernel kernel)
        {
            var descriptions = new KernelText(kernel.Sections.ElementAt(12));
            var names = new KernelText(kernel.Sections.ElementAt(20));

            var data = new MemoryStream(kernel.Sections.ElementAt(5));

            int index = 0;
            while (data.Position < data.Length)
            {
                Weapon weapon = new Weapon
                {
                    Name = names.Get(index),
                    Description = descriptions.Get(index),
                    ID = index,
                };
                index++;
                weapon.TargettingFlags = (TargettingFlags)data.ReadU8();
                data.ReadU8();
                weapon.DamageFormula = data.ReadU8();
                data.ReadU8();
                weapon.AttackStrength = data.ReadU8();
                byte status = data.ReadU8();
                weapon.Statuses = status == 0xff ? Statuses.None : (Statuses)(1 << status);
                weapon.Growth = data.ReadU8();
                if (weapon.Growth > 3) weapon.Growth = 1;
                weapon.CriticalChance = data.ReadU8();
                weapon.HitChance = data.ReadU8();
                byte model = data.ReadU8();
                weapon.AnimationModifier = (byte)(model >> 4);
                weapon.AttackModel = (byte)(model & 0xf);
                data.ReadU8();
                byte hiSound = data.ReadU8();
                hiSound = 0;
                data.ReadU16();
                weapon.EquippableOn = data.ReadU16();
                weapon.Elements = (Elements)data.ReadU16();
                data.ReadU16();

                weapon.ApplyStatModifiers(new StatModifiers(data.ReadU32(), data.ReadU32()));

                foreach (int _ in Enumerable.Range(0, 8))
                {
                    switch (data.ReadU8())
                    {
                        case 1:
                        case 5:
                            weapon.MateriaSlots.Add(MateriaSlotKind.Single);
                            break;
                        case 2:
                        case 3:
                        case 6:
                        case 7:
                            weapon.MateriaSlots.Add(MateriaSlotKind.Linked);
                            break;
                    }
                }

                weapon.HitSoundEffect = (hiSound << 8) | data.ReadU8();
                weapon.CriticalSoundEffect = (hiSound << 8) | data.ReadU8();
                weapon.MissSoundEffect = (hiSound << 8) | data.ReadU8();

                if ((hiSound & 1) != 0)
                    weapon.HitSoundEffect += 254;
                if ((hiSound & 2) != 0)
                    weapon.CriticalSoundEffect += 254;
                if ((hiSound & 4) != 0)
                    weapon.MissSoundEffect += 254;

                weapon.ImpactEffectID = data.ReadU8();
                data.ReadU16();
                weapon.Restrictions = (EquipRestrictions)(~data.ReadU16() & 0xf);

                _weapons.Add(weapon);
            }
        }


    }
}
