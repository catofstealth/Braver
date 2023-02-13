﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Braver {

    public abstract class DataSource {
        public abstract Stream TryOpen(string file);
        public abstract IEnumerable<string> Scan();
    }

    public abstract class BGame {

        protected class LGPDataSource : DataSource {
            private Ficedula.FF7.LGPFile _lgp;

            public LGPDataSource(Ficedula.FF7.LGPFile lgp) {
                _lgp = lgp;
            }

            public override IEnumerable<string> Scan() => _lgp.Filenames;
            public override Stream TryOpen(string file) => _lgp.TryOpen(file);
        }

        protected class FileDataSource : DataSource {
            private string _root;

            public FileDataSource(string root) {
                _root = root;
            }

            public override IEnumerable<string> Scan() {
                //TODO subdirectories
                return Directory.GetFiles(_root).Select(s => Path.GetFileName(s));
            }

            public override Stream TryOpen(string file) {
                string fn = Path.Combine(_root, file);
                if (File.Exists(fn))
                    return new FileStream(fn, FileMode.Open, FileAccess.Read);
                return null;
            }
        }
        public VMM Memory { get; } = new();
        public SaveMap SaveMap { get; }

        public SaveData SaveData { get; protected set; }
        protected Dictionary<string, List<DataSource>> _data = new Dictionary<string, List<DataSource>>(StringComparer.InvariantCultureIgnoreCase);
        private Dictionary<Type, object> _singletons = new();

        public BGame() {
            SaveMap = new SaveMap(Memory);
        }

        public int GameTimeSeconds {
            get => SaveMap.GameTimeSeconds + 60 * SaveMap.GameTimeMinutes + 60 * 60 * SaveMap.GameTimeHours;
            set {
                int v = value;
                SaveMap.GameTimeSeconds = (byte)(v % 60);
                v /= 60;
                SaveMap.GameTimeMinutes = (byte)(v % 60);
                v /= 60;
                SaveMap.GameTimeHours = (byte)(v % 60);
            }
        }
        public int CounterSeconds {
            get => SaveMap.CounterSeconds + 60 * SaveMap.CounterMinutes + 60 * 60 * SaveMap.CounterHours;
            set {
                int v = value;
                SaveMap.CounterSeconds = (byte)(v % 60);
                v /= 60;
                SaveMap.CounterMinutes = (byte)(v % 60);
                v /= 60;
                SaveMap.CounterHours = (byte)(v % 60);
            }
        }

        protected void FrameIncrement() {
            if (++SaveMap.GameTimeFrames >= 30) {
                SaveMap.GameTimeFrames = 0;
                GameTimeSeconds++;
            }
            if (CounterSeconds > 0) {
                if (++SaveMap.CounterFrames >= 30) {
                    SaveMap.CounterFrames = 0;
                    CounterSeconds--;
                }
            }
        }

        public void Save(string path) {
            Directory.CreateDirectory(Path.GetDirectoryName(path + ".sav"));
            using (var fs = File.Create(path + ".sav"))
                Serialisation.Serialise(SaveData, fs);
            using (var fs = File.Create(path + ".mem"))
                Memory.Save(fs);
        }

        public virtual void Load(string path) {
            using (var fs = File.OpenRead(path + ".mem"))
                Memory.Load(fs);
            using (var fs = File.OpenRead(path + ".sav"))
                SaveData = Serialisation.Deserialise<SaveData>(fs);
            SaveData.CleanUp();
        }

        public T Singleton<T>() where T : Cacheable, new() {
            return Singleton<T>(() => {
                T t = new T();
                t.Init(this);
                return t;
            });
        }

        public T Singleton<T>(Func<T> create) {
            if (_singletons.TryGetValue(typeof(T), out object obj))
                return (T)obj;
            else {
                T t = create();
                _singletons[typeof(T)] = t;
                return t;
            }
        }


        public void NewGame() {
            using (var s = Open("save", "newgame.xml"))
                SaveData = Serialisation.Deserialise<SaveData>(s);
            Memory.ResetAll();
            Braver.NewGame.Init(this);
            SaveData.CleanUp();
        }

        public IEnumerable<string> ScanData(string category) {
            if (_data.TryGetValue(category, out var sources))
                return sources.SelectMany(s => s.Scan());
            else
                return Enumerable.Empty<string>();
        }

        public Stream TryOpen(string category, string file) {
            foreach (var source in _data[category]) {
                var s = source.TryOpen(file);
                if (s != null)
                    return s;
            }
            return null;
        }

        public Stream Open(string category, string file) {
            var s = TryOpen(category, file);
            if (s == null)
                throw new F7Exception($"Could not open {category}/{file}");
            else
                return s;
        }
        public string OpenString(string category, string file) {
            using (var s = Open(category, file)) {
                using (var sr = new StreamReader(s))
                    return sr.ReadToEnd();
            }
        }

    }

    public abstract class Cacheable {
        public abstract void Init(BGame g);
    }

}
