﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ficedula.FF7.Battle {

    [Flags]
    public enum TargetFlags : byte {
        None = 0,
        EnableSelection = 0x1,
        StartWithEnemy = 0x2,
        DefaultMultiTarget = 0x4,
        ToggleMultiSingle = 0x8,
        OneRowOnly = 0x10,
        ShortRange = 0x20,
        AllRows = 0x40,
        RandomTarget = 0x80,
    }


    public enum AttackCondition : byte {
        PartyHP = 0,
        PartyMP = 1,
        PartyStatus = 2,
    }

    public enum AttackStatusType {
        Inflict,
        Cure,
        Toggle,
    }


    [Flags]
    public enum SpecialAttackFlags : ushort {
        DamageMP = 0x1,
        Unused1 = 0x2,
        AffectedByDarkness = 0x4,
        Unused2 = 0x8,
        DrainsDamage = 0x10,
        DrainsHPandMP = 0x20,
        Unused3 = 0x40,
        IgnoreStatusEffectDefense = 0x80,
        MissIfTargetNotDead = 0x100,
        Reflectable = 0x200,
        IgnoreDefenseCalc = 0x400,
        DoNotRetargetIfTargetDead = 0x800,
        Unused4 = 0x1000,
        AlwaysCritical = 0x2000,
        Unused5 = 0x4000,
        Unused6 = 0x8000,
    }

    public class Attack {
        public byte AttackPC { get; set; }
        public byte ImpactEffect { get; set; }
        public byte TargetHurtAction { get; set; }
        public short CastingCost { get; set; }
        public short ImpactSound { get; set; }
        public short SingleTargetCameraID { get; set; }
        public short MultiTargetCameraID { get; set; }
        public TargetFlags TargetFlags { get; set; }
        public byte AttackEffectID { get; set; }
        public byte DamageType { get; set; } //TODO - decode
        public byte Power { get; set; }
        public AttackCondition AttackCondition { get; set; }

        public byte StatusChance { get; set; } //out of 64
        public AttackStatusType StatusType { get; set; }
        public byte AdditionalEffects { get; set; }
        public byte AdditionalEffectsParam { get; set; }
        public Statuses Statuses { get; set; }
        public Elements Elements { get; set; }
        public SpecialAttackFlags SpecialAttackFlags { get; set; }

        //Not part of standard record
        public short ActionID { get; set; }
        public string Name { get; set; }

        public Attack() { }
        public Attack(Stream s) {
            AttackPC = (byte)s.ReadByte();
            ImpactEffect = (byte)s.ReadByte();
            TargetHurtAction = (byte)s.ReadByte();
            s.ReadByte();
            CastingCost = s.ReadI16();
            ImpactSound = s.ReadI16();
            SingleTargetCameraID = s.ReadI16();
            MultiTargetCameraID = s.ReadI16();
            TargetFlags = (TargetFlags)s.ReadByte();
            AttackEffectID = (byte)s.ReadByte();
            DamageType = (byte)s.ReadByte();
            Power = (byte)s.ReadByte();
            AttackCondition = (AttackCondition)s.ReadByte();
            StatusChance = (byte)s.ReadByte();
            StatusType = ((StatusChance & 0x40) != 0) ? AttackStatusType.Cure :
                ((StatusChance & 0x80) != 0) ? AttackStatusType.Toggle :
                AttackStatusType.Inflict;
            StatusChance &= 0x3f;
            AdditionalEffects = (byte)s.ReadByte();
            AdditionalEffectsParam= (byte)s.ReadByte();
            Statuses = (Statuses)s.ReadI32();
            Elements = (Elements)s.ReadU16();
            SpecialAttackFlags = (SpecialAttackFlags)~s.ReadU16();            
        }
    }

    public class AttackCollection {
        private List<Attack> _attacks = new();

        public System.Collections.ObjectModel.ReadOnlyCollection<Attack> Attacks => _attacks.AsReadOnly();

        public AttackCollection(Stream s) {
            while(s.Position < s.Length) {
                var attack = new Attack(s);
                _attacks.Add(attack);
            }
        }
    }
}
