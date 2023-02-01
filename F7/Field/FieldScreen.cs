﻿using Ficedula.FF7.Field;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Field {

    public class BattleOptions {
        public string OverrideMusic { get; set; }
        public string PostBattleMusic { get; set; } //will play in field
        public bool BattlesEnabled { get; set; } = true; //TODO - reasonable default?
        public Battle.BattleFlags Flags { get; set; } = Battle.BattleFlags.None;
    }

    public class FieldInfo {
        public float BGZFrom { get; set; }
        public float BGZTo { get; set; }
    }

    public class FieldLine {
        public Vector3 P0 { get; set; }
        public Vector3 P1 { get; set; }
        public bool Active { get; set; } = true;

        public bool IntersectsWith(Vector3 entity, float entityRadius) {
            if (!Active)
                return false;

            return GraphicsUtil.LineCircleIntersect(P0.XY(), P1.XY(), entity.XY(), entityRadius);
        }
    }

    [Flags]
    public enum FieldOptions {
        None = 0,
        PlayerControls = 0x1,
        //LinesActive = 0x2,
        MenuEnabled = 0x4, 
        CameraTracksPlayer = 0x8,
        CameraIsAsyncScrolling = 0x10,
        MusicLocked = 0x20,

        DEFAULT = PlayerControls | MenuEnabled | CameraTracksPlayer,  
    }

    public class FieldScreen : Screen, Net.IListen<Net.FieldModelMessage>, Net.IListen<Net.FieldBGMessage>,
        Net.IListen<Net.FieldEntityModelMessage>, Net.IListen<Net.FieldBGScrollMessage> {

        private PerspView3D _view3D;
        private Vector3 _camRight;
        private View2D _view2D;
        private FieldDebug _debug;
        private FieldInfo _info;
        private float _bgZFrom = 1025f, _bgZTo = 1092f;

        private bool _debugMode = false;
        private bool _renderBG = true, _renderDebug = true, _renderModels = true;
        private float _controlRotation;

        private List<WalkmeshTriangle> _walkmesh;

        private TriggersAndGateways _triggersAndGateways;

        public override Color ClearColor => Color.Black;

        public Entity Player { get; private set; }

        public Action WhenPlayerSet { get; set; }

        public HashSet<int> DisabledWalkmeshTriangles { get; } = new HashSet<int>();
        public Background Background { get; private set; }
        public Movie Movie { get; private set; }
        public List<Entity> Entities { get; private set; }
        public List<FieldModel> FieldModels { get; private set; }
        public DialogEvent FieldDialog { get; private set; }

        private EncounterTable[] _encounters;
        public FieldOptions Options { get; set; } = FieldOptions.DEFAULT;
        public Dialog Dialog { get; private set; }
        public Overlay Overlay { get; private set; }

        public int BattleTable { get; set; }
        public BattleOptions BattleOptions { get; } = new();

        private HashSet<Trigger> _activeTriggers = new();

        private FieldDestination _destination;
        private short _fieldID;
        public FieldScreen(FieldDestination destination) {
            _destination = destination;
            _fieldID = destination.DestinationFieldID;
        }

        private void SetPlayerIfNecessary() {
            if (Player == null) {
                var autoPlayer = Entities
                    .Where(e => e.Character != null)
                    .Where(e => e.Model?.Visible == true)
                    .FirstOrDefault();
                if (autoPlayer != null)
                    SetPlayer(Entities.IndexOf(autoPlayer));
            }
        }

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);

            UpdateSaveLocation();
            if (g.DebugOptions.AutoSaveOnFieldEntry)
                Game.Save(System.IO.Path.Combine(FGame.GetSavePath(), "auto"));


            g.Net.Listen<Net.FieldModelMessage>(this);
            g.Net.Listen<Net.FieldBGMessage>(this);
            g.Net.Listen<Net.FieldEntityModelMessage>(this);
            g.Net.Listen<Net.FieldBGScrollMessage>(this);

            Overlay = new Overlay(g, graphics);

            g.Net.Send(new Net.FieldScreenMessage { Destination = _destination });

            FieldFile field;

            var mapList = g.Singleton(() => new MapList(g.Open("field", "maplist")));
            string file = mapList.Items[_destination.DestinationFieldID];
            var cached = g.Singleton(() => new CachedField());
            if (cached.FieldID == _destination.DestinationFieldID)
                field = cached.FieldFile;
            else {
                using (var s = g.Open("field", file))
                    field = new FieldFile(s);
            }

            Background = new Background(g, graphics, field.GetBackground());
            Movie = new Movie(g, graphics);
            FieldDialog = field.GetDialogEvent();
            _encounters = field.GetEncounterTables().ToArray();

            Entities = FieldDialog.Entities
                .Select(e => new Entity(e, this))
                .ToList();

            FieldModels = field.GetModels()
                .Models
                .Select((m, index) => {
                    var model = new FieldModel(
                        graphics, g, index, m.HRC,
                        m.Animations.Select(s => System.IO.Path.ChangeExtension(s, ".a"))
                    ) {
                        Scale = float.Parse(m.Scale) / 128f,
                        Rotation2 = new Vector3(0, 0, 0),
                    };
                    model.Translation2 = new Vector3(
                        0,
                        0,
                        model.Scale * model.MaxBounds.Y
                    );
                    return model;
                })
                .ToList();

            _triggersAndGateways = field.GetTriggersAndGateways();
            _controlRotation = 360f * _triggersAndGateways.ControlDirection / 256f;

            _walkmesh = field.GetWalkmesh().Triangles;

            using (var sinfo = g.TryOpen("field", file + ".xml")) {
                if (sinfo != null) {
                    _info = Serialisation.Deserialise<FieldInfo>(sinfo);
                } else
                    _info = new FieldInfo();
            }

            var cam = field.GetCameraMatrices().First();

            /*
            float camWidth, camHeight;
            if (_info.Cameras.Any()) {
                camWidth = _info.Cameras[0].Width;
                camHeight = _info.Cameras[0].Height;
                _base3DOffset = new Vector2(_info.Cameras[0].CenterX, _info.Cameras[0].CenterY);
            } else {
                //Autodetect...
                var testCam = new OrthoView3D {
                    CameraPosition = new Vector3(cam.CameraPosition.X, cam.CameraPosition.Z, cam.CameraPosition.Y),
                    CameraForwards = new Vector3(cam.Forwards.X, cam.Forwards.Z, cam.Forwards.Y),
                    CameraUp = new Vector3(cam.Up.X, cam.Up.Z, cam.Up.Y),
                    Width = 1280,
                    Height = 720,
                };
                var vp = testCam.View * testCam.Projection;

                Vector3 Project(FieldVertex v) {
                    return Vector3.Transform(new Vector3(v.X, v.Y, v.Z), vp);
                }

                Vector3 vMin, vMax;
                vMin = vMax = Project(_walkmesh[0].V0);

                foreach(var wTri in _walkmesh) {
                    Vector3 v0 = Vector3.Transform(new Vector3(wTri.V0.X, wTri.V0.Y, wTri.V0.Z), vp),
                        v1 = Vector3.Transform(new Vector3(wTri.V1.X, wTri.V1.Y, wTri.V1.Z), vp),
                        v2 = Vector3.Transform(new Vector3(wTri.V2.X, wTri.V2.Y, wTri.V2.Z), vp);
                    vMin = new Vector3(
                        Math.Min(vMin.X, Math.Min(Math.Min(v0.X, v1.X), v2.X)),
                        Math.Min(vMin.Y, Math.Min(Math.Min(v0.Y, v1.Y), v2.Y)),
                        Math.Min(vMin.Z, Math.Min(Math.Min(v0.Z, v1.Z), v2.Z))
                    );
                    vMax = new Vector3(
                        Math.Max(vMax.X, Math.Max(Math.Max(v0.X, v1.X), v2.X)),
                        Math.Max(vMax.Y, Math.Max(Math.Max(v0.Y, v1.Y), v2.Y)),
                        Math.Max(vMax.Z, Math.Max(Math.Max(v0.Z, v1.Z), v2.Z))
                    );
                }

                var allW = _walkmesh.SelectMany(t => new[] { t.V0, t.V1, t.V2 });
                Vector3 wMin = new Vector3(allW.Min(v => v.X), allW.Min(v => v.Y), allW.Min(v => v.Z)),
                    wMax = new Vector3(allW.Max(v => v.X), allW.Max(v => v.Y), allW.Max(v => v.Z)); 

                float xRange = (vMax.X - vMin.X) * 0.5f,
                    yRange = (vMax.Y - vMin.Y) * 0.5f;

                //So now we know the walkmap would cover xRange screens across and yRange screens down
                //Compare that to the background width/height and scale it to match...

                System.Diagnostics.Debug.WriteLine($"Walkmap range {wMin} - {wMax}");
                System.Diagnostics.Debug.WriteLine($"Transformed {vMin} - {vMax}");
                System.Diagnostics.Debug.WriteLine($"Walkmap covers range {xRange}/{yRange}");
                System.Diagnostics.Debug.WriteLine($"Background is size {Background.Width} x {Background.Height}");
                System.Diagnostics.Debug.WriteLine($"Background covers {Background.Width / 320f} x {Background.Height / 240f} screens");
                System.Diagnostics.Debug.WriteLine($"...or in widescreen, {Background.Width / 427f} x {Background.Height / 240f} screens");

                camWidth = 1280f * xRange / (Background.Width / 320f);
                camHeight = 720f * yRange / (Background.Height / 240f);
                System.Diagnostics.Debug.WriteLine($"Auto calculated ortho w/h to {camWidth}/{camHeight}");

                camWidth = 1280f * xRange / (Background.Width / 427f);
                camHeight = 720f * yRange / (Background.Height / 240f);
                System.Diagnostics.Debug.WriteLine($"...or in widescreen, {camWidth}/{camHeight}");

                _base3DOffset = Vector2.Zero;
            }

            _view3D = new OrthoView3D {
                CameraPosition = new Vector3(cam.CameraPosition.X, cam.CameraPosition.Z, cam.CameraPosition.Y),
                CameraForwards = new Vector3(cam.Forwards.X, cam.Forwards.Z, cam.Forwards.Y),
                CameraUp = new Vector3(cam.Up.X, cam.Up.Z, cam.Up.Y),
                Width = camWidth,
                Height = camHeight,
                CenterX = _base3DOffset.X,
                CenterY = _base3DOffset.Y,
            };
            */

            double fovy = (2 * Math.Atan(240.0 / (2.0 * cam.Zoom))) * 57.29577951;

            var camPosition = cam.CameraPosition.ToX() * 4096f;

            var camDistances = _walkmesh
                .SelectMany(tri => new[] { tri.V0.ToX(), tri.V1.ToX(), tri.V2.ToX() })
                .Select(v => (camPosition - v).Length());

            float nearest = camDistances.Min(), furthest = camDistances.Max();

            _view3D = new PerspView3D {
                FOV = (float)fovy,
                ZNear = nearest * 0.75f,
                ZFar = furthest * 1.25f,
                CameraPosition = camPosition,
                CameraForwards = cam.Forwards.ToX(),
                CameraUp = cam.Up.ToX(),
            };
            _camRight = cam.Right.ToX();

            var vp = System.Numerics.Matrix4x4.CreateLookAt(
                cam.CameraPosition * 4096f, cam.CameraPosition * 4096f + cam.Forwards, cam.Up
            ) * System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView(
                (float)(fovy * Math.PI / 180.0), 1280f / 720f, 0.001f * 4096f, 1000f * 4096f
            );

            float minZ = 1f, maxZ = 0f;
            foreach (var wTri in _walkmesh) {
                System.Numerics.Vector4 v0 = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(wTri.V0.X, wTri.V0.Y, wTri.V0.Z, 1), vp),
                    v1 = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(wTri.V1.X, wTri.V1.Y, wTri.V1.Z, 1), vp),
                    v2 = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(wTri.V2.X, wTri.V2.Y, wTri.V2.Z, 1), vp);
                /*
                System.Diagnostics.Debug.WriteLine(v0 / v0.W);
                System.Diagnostics.Debug.WriteLine(v1 / v1.W);
                System.Diagnostics.Debug.WriteLine(v2 / v2.W);
                */
                minZ = Math.Min(minZ, v0.Z / v0.W);
                minZ = Math.Min(minZ, v1.Z / v1.W);
                minZ = Math.Min(minZ, v2.Z / v2.W);
                maxZ = Math.Max(maxZ, v0.Z / v0.W);
                maxZ = Math.Max(maxZ, v1.Z / v1.W);
                maxZ = Math.Max(maxZ, v2.Z / v2.W);
            }
            System.Diagnostics.Debug.WriteLine($"Walkmesh Z varies from {minZ}-{maxZ} (recip {1f / minZ} to {1f / maxZ}");
            _debug = new FieldDebug(graphics, field);

            if (_info.BGZFrom != 0) {
                _bgZFrom = _info.BGZFrom;
                _bgZTo = _info.BGZTo;
            } else {
                _bgZFrom = Background.AutoDetectZFrom;
                _bgZTo = Background.AutoDetectZTo;
            }

            Dialog = new Dialog(g, graphics);

            g.Memory.ResetScratch();

            _view2D = new View2D {
                Width = 1280,
                Height = 720,
                ZNear = 0,
                ZFar = -1,
            };

            if (g.Net is Net.Server) {
                if (!Game.DebugOptions.NoFieldScripts) {
                    foreach (var entity in Entities) {
                        entity.Call(0, 0, null);
                        entity.Run(9999, true);
                    }
                }
                SetPlayerIfNecessary(); //TODO - is it OK to delay doing this? But until the entity scripts run we don't know which entity corresponds to which party member...

                BringPlayerIntoView();

                if (!Overlay.HasTriggered)
                    Overlay.Fade(30, GraphicsUtil.BlendSubtractive, Color.White, Color.Black, null);

                g.Net.Send(new Net.ScreenReadyMessage());
            }
            Entity.DEBUG_OUT = false;
        }

        private int _nextModelIndex = 0;
        public int GetNextModelIndex() {
            return _nextModelIndex++;
        }

        protected override void DoRender() {
            Graphics.DepthStencilState = DepthStencilState.Default;
            Graphics.BlendState = BlendState.AlphaBlend;
            if (_renderBG) {
                if (Movie.Active)
                    Movie.Render();
                else
                    Background.Render(_view2D, _bgZFrom, _bgZTo);
            }

            if (_renderDebug)
                _debug.Render(_view3D);

            if (_renderModels) {
                using (var state = new GraphicsState(Graphics, rasterizerState: RasterizerState.CullClockwise)) {
                    foreach (var entity in Entities)
                        if ((entity.Model != null) && entity.Model.Visible)
                            entity.Model.Render(_view3D);
                }
            }

            Overlay.Render();

            Dialog.Render();
        }

        private class FrameProcess {
            public int Frames;
            public Func<int, bool> Process;
        }

        private List<FrameProcess> _processes = new();

        public void StartProcess(Func<int, bool> process) {
            _processes.Add(new FrameProcess { Process = process });
        }

        private int _frame = 0;
        protected override void DoStep(GameTime elapsed) {
            if (Game.Net is Net.Server) {
                if ((_frame % 2) == 0) {
                    Overlay.Step();
                    foreach (var entity in Entities) {
                        if (!Game.DebugOptions.NoFieldScripts) 
                            entity.Run(1000);
                        entity.Model?.FrameStep();
                    }
                }

                for (int i = _processes.Count - 1; i >= 0; i--) {
                    var process = _processes[i];
                    if (process.Process(process.Frames++))
                        _processes.RemoveAt(i);
                }

                Dialog.Step();
                Movie.Step();
                Background.Step();
            } else {
                if ((_frame % 2) == 0) {
                    foreach (var entity in Entities)
                        entity.Model?.FrameStep();
                }
            }
            _frame++;
        }

        public (int x, int y) GetBGScroll() {
            return (
                (int)(-_view2D.CenterX / 3),
                (int)(_view2D.CenterY / 3)
            );
        }
        public void BGScroll(float x, float y) {
            BGScrollOffset(x - (-_view2D.CenterX / 3), y - (_view2D.CenterY / 3));
        }
        public void BGScrollOffset(float ox, float oy) {
            _view2D.CenterX -= 3 * ox;
            _view2D.CenterY += 3 * oy;

            var newScroll = GetBGScroll();
            _view3D.ScreenOffset = new Vector2(newScroll.x * 3f * 2 / 1280, newScroll.y * -3f * 2 / 720);

            Game.Net.Send(new Net.FieldBGScrollMessage {
                X = _view2D.CenterX / 3,
                Y = _view2D.CenterY / 3,
            });
        }

        private void ReportAllModelPositions() {
            foreach(var entity in Entities.Where(e => e.Model != null)) {
                System.Diagnostics.Debug.WriteLine($"Entity {entity.Name} at pos {entity.Model.Translation}, 2D background pos {ModelToBGPosition(entity.Model.Translation)}");
            }
        }

        public Vector2 ClampBGScrollToViewport(Vector2 bgScroll) {
            int minX, maxX, minY, maxY;

            if (Background.Width < (1280f / 3))
                minX = maxX = 0;
            else {
                minX = Background.MinX + (1280 / 3) / 2;
                maxX = (Background.MinX + Background.Width) - (1280 / 3) / 2;
            }

            if (Background.Height < (720f / 3))
                minY = maxY = 0;
            else {
                minY = Background.MinY + (720 / 3) / 2;
                maxY = (Background.MinY + Background.Height) - (730 / 3) / 2;
            }

            return new Vector2(
                Math.Min(Math.Max(minX, bgScroll.X), maxX),
                Math.Min(Math.Max(minY, bgScroll.Y), maxY)
            );
        }

        private void BringPlayerIntoView() {
            if (Player != null) {
                var posOnBG = ModelToBGPosition(Player.Model.Translation);
                var scroll = GetBGScroll();
                var newScroll = scroll;
                if (posOnBG.X > (scroll.x + 150))
                    newScroll.x = (int)posOnBG.X - 150;
                else if (posOnBG.X < (scroll.x - 150))
                    newScroll.x = (int)posOnBG.X + 150;

                if (posOnBG.Y > (scroll.y + 100))
                    newScroll.y = (int)posOnBG.Y - 100;
                else if (posOnBG.Y < (scroll.y - 110))
                    newScroll.y = (int)posOnBG.Y + 110;

                if (newScroll != scroll) {
                    System.Diagnostics.Debug.WriteLine($"BringPlayerIntoView: Player at BG pos {posOnBG}, BG scroll is {scroll}, needs to be {newScroll}");
                    BGScroll(newScroll.x, newScroll.y);
                }
            }
        }

        public Vector2 ModelToBGPosition(Vector3 modelPosition, Matrix? transformMatrix = null, bool debug = false) {
            transformMatrix ??= _view3D.View * _view3D.Projection;
            var screenPos = Vector4.Transform(modelPosition, transformMatrix.Value);
            screenPos = screenPos / screenPos.W;

            float tx = (_view2D.CenterX / 3) + screenPos.X * 0.5f * (1280f / 3),
                  ty = (_view2D.CenterY / 3) + screenPos.Y * 0.5f * (720f / 3);

            if (debug)
                System.Diagnostics.Debug.WriteLine($"ModelToBG: {modelPosition} -> screen {screenPos} -> BG {tx}/{ty}");

            return new Vector2(-tx, ty);
        }

        private InputState _lastInput;

        internal InputState LastInput => _lastInput;

        private void UpdateSaveLocation() {
            Game.SaveData.Module = Module.Field;
            Game.SaveData.FieldDestination = _destination ?? new FieldDestination {
                Triangle = (ushort)Player.WalkmeshTri,
                X = (short)Player.Model.Translation.X,
                Y = (short)Player.Model.Translation.Y,
                Orientation = (byte)(Player.Model.Rotation.Y * 255 / 360),
                DestinationFieldID = _fieldID,
            };
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);
            if (!(Game.Net is Net.Server)) return;

            _lastInput = input;
            if (input.IsJustDown(InputKey.Start))
                _debugMode = !_debugMode;

            if (input.IsJustDown(InputKey.Debug1))
                _renderBG = !_renderBG;
            if (input.IsJustDown(InputKey.Debug2))
                _renderDebug = !_renderDebug;
            if (input.IsJustDown(InputKey.Debug3)) {
                _renderModels = !_renderModels;
                ReportAllModelPositions();
            }

            if (input.IsJustDown(InputKey.Debug5))
                Entity.DEBUG_OUT = !Entity.DEBUG_OUT;


            if (_debugMode) {

                if (input.IsDown(InputKey.PanLeft))
                    BGScrollOffset(0, -1);
                else if (input.IsDown(InputKey.PanRight))
                    BGScrollOffset(0, +1);

                if (input.IsAnyDirectionDown() || input.IsJustDown(InputKey.Select)) {

                    if (input.IsDown(InputKey.Up))
                        _view3D.CameraPosition += _view3D.CameraUp;
                    else if (input.IsDown(InputKey.Down))
                        _view3D.CameraPosition -= _view3D.CameraUp;

                    System.Diagnostics.Debug.WriteLine($"Player at {ModelToBGPosition(Player.Model.Translation, null, false)} WM0 {ModelToBGPosition(_walkmesh[0].V0.ToX(), null, false)}");

                    /*
                    //Now calculate 3d scroll amount
                    var _3dScrollAmount = new Vector2(_view3D.Width / 427f, _view3D.Height / 240f);
                    System.Diagnostics.Debug.WriteLine($"To scroll 3d view by one BG pixel, it will move {_3dScrollAmount}");

                    if (input.IsJustDown(InputKey.Select)) {
                        _view3D.CenterX += _3dScrollAmount.X * _view2D.CenterX / -3;
                        _view3D.CenterY += _3dScrollAmount.Y * _view2D.CenterY / -3;
                    }

                    if (input.IsDown(InputKey.OK)) {
                        if (input.IsDown(InputKey.Up)) {
                            _view2D.CenterY += 3;
                            _view3D.CenterY += _3dScrollAmount.Y;
                        }
                        if (input.IsDown(InputKey.Down)) {
                            _view2D.CenterY -= 3;
                            _view3D.CenterY -= _3dScrollAmount.Y;
                        }
                        if (input.IsDown(InputKey.Left)) {
                            _view2D.CenterX -= 3;
                            _view3D.CenterX -= _3dScrollAmount.X;
                        }
                        if (input.IsDown(InputKey.Right)) {
                            _view2D.CenterX += 3;
                            _view3D.CenterX += _3dScrollAmount.X;
                        }

                    } else if (input.IsDown(InputKey.Cancel)) {
                        if (input.IsDown(InputKey.Up))
                            _view2D.CenterY++;
                        if (input.IsDown(InputKey.Down))
                            _view2D.CenterY--;
                        if (input.IsDown(InputKey.Left))
                            _view2D.CenterX--;
                        if (input.IsDown(InputKey.Right))
                            _view2D.CenterX++;

                    } else {
                        if (input.IsDown(InputKey.Menu)) {

                            if (input.IsDown(InputKey.Up))
                                _view3D.Height++;
                            if (input.IsDown(InputKey.Down))
                                _view3D.Height--;
                            if (input.IsDown(InputKey.Left))
                                _view3D.Width--;
                            if (input.IsDown(InputKey.Right))
                                _view3D.Width++;

                        } else {
                            if (input.IsDown(InputKey.Up)) {
                                _view3D.CenterY++;
                            }
                            if (input.IsDown(InputKey.Down)) {
                                _view3D.CenterY--;
                            }
                            if (input.IsDown(InputKey.Left)) {
                                _view3D.CenterX--;
                            }
                            if (input.IsDown(InputKey.Right)) {
                                _view3D.CenterX++;
                            }
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"View2D Center: {_view2D.CenterX}/{_view2D.CenterY}");
                    System.Diagnostics.Debug.WriteLine($"View3D: {_view3D}");
                    */
                }
            } else {

                if (Dialog.IsActive) {
                    Dialog.ProcessInput(input);
                    return;
                }

                if (input.IsJustDown(InputKey.Menu) && Options.HasFlag(FieldOptions.MenuEnabled)) {
                    UpdateSaveLocation();
                    Game.PushScreen(new UI.Layout.LayoutScreen("MainMenu"));
                    return;
                }

                //Normal controls
                if ((Player != null) && Options.HasFlag(FieldOptions.PlayerControls)) {

                    if (input.IsJustDown(InputKey.OK) && (Player != null)) {
                        var talkTo = Player.CollidingWith
                            .Where(e => e.Flags.HasFlag(EntityFlags.CanTalk))
                            .FirstOrDefault();
                        if (talkTo != null) {
                            if (!talkTo.Call(7, 1, null))
                                System.Diagnostics.Debug.WriteLine($"Could not start talk script for entity {talkTo}");
                        }
                    }

                    int desiredAnim = 0;
                    float animSpeed = 1f;
                    if (input.IsAnyDirectionDown() && Options.HasFlag(FieldOptions.PlayerControls)) {
                        //TODO actual use controldirection
                        var forwards = _view3D.CameraForwards.WithZ(0);
                        forwards.Normalize();
                        var right = Vector3.Transform(forwards, Matrix.CreateRotationZ(90f * (float)Math.PI / 180f));
                        var move = Vector2.Zero;

                        if (input.IsDown(InputKey.Up))
                            move += new Vector2(forwards.X, forwards.Y);
                        else if (input.IsDown(InputKey.Down))
                            move -= new Vector2(forwards.X, forwards.Y);

                        if (input.IsDown(InputKey.Left))
                            move += new Vector2(right.X, right.Y);
                        else if (input.IsDown(InputKey.Right))
                            move -= new Vector2(right.X, right.Y);

                        if (move != Vector2.Zero) {
                            move.Normalize();
                            move *= 2;
                            if (input.IsDown(InputKey.Cancel)) {
                                animSpeed = 2f;
                                move *= 4f;
                                desiredAnim = 2;
                            } else
                                desiredAnim = 1;

                            TryWalk(Player, Player.Model.Translation + new Vector3(move.X, move.Y, 0), true);
                            Player.Model.Rotation = Player.Model.Rotation.WithZ((float)(Math.Atan2(move.X, -move.Y) * 180f / Math.PI));

                            var oldLines = Player.LinesCollidingWith.ToArray();
                            Player.LinesCollidingWith.Clear();
                            foreach (var lineEnt in Entities.Where(e => e.Line != null)) {
                                if (lineEnt.Line.IntersectsWith(Player.Model.Translation, Player.CollideDistance))
                                    Player.LinesCollidingWith.Add(lineEnt);
                            }

                            foreach(var entered in Player.LinesCollidingWith.Except(oldLines)) {
                                System.Diagnostics.Debug.WriteLine($"Player has entered line {entered}");
                                entered.Call(3, 5, null); //TODO PRIORITY!?!
                            }

                            foreach (var left in oldLines.Except(Player.LinesCollidingWith)) {
                                System.Diagnostics.Debug.WriteLine($"Player has left line {left}");
                                left.Call(3, 6, null); //TODO PRIORITY!?!
                            }

                            foreach (var gateway in _triggersAndGateways.Gateways) {
                                if (GraphicsUtil.LineCircleIntersect(gateway.V0.ToX().XY(), gateway.V1.ToX().XY(), Player.Model.Translation.XY(), Player.CollideDistance)) {
                                    Options &= ~FieldOptions.PlayerControls;
                                    desiredAnim = 0; //stop player walking as they won't move any more!
                                    FadeOut(() => {
                                        Game.ChangeScreen(this, new FieldScreen(gateway.Destination));
                                    });
                                }
                            }
                            foreach(var trigger in _triggersAndGateways.Triggers) {
                                bool active = GraphicsUtil.LineCircleIntersect(trigger.V0.ToX().XY(), trigger.V1.ToX().XY(), Player.Model.Translation.XY(), Player.CollideDistance);
                                if (active != _activeTriggers.Contains(trigger)) {

                                    bool setOn = false, setOff = false;
                                    switch (trigger.Behaviour) {
                                        case TriggerBehaviour.OnNone:
                                            if (active) 
                                                setOn = true;
                                            break;
                                        case TriggerBehaviour.OffNone:
                                            if (active)
                                                setOff = true;
                                            break;
                                        case TriggerBehaviour.OnOff:
                                        case TriggerBehaviour.OnOffPlus: //TODO - plus side only
                                            setOn = active;
                                            setOff = !active;
                                            break;
                                        case TriggerBehaviour.OffOn:
                                        case TriggerBehaviour.OffOnPlus: //TODO - plus side only
                                            setOn = !active;
                                            setOff = active;
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }

                                    if (setOn)
                                        Background.ModifyParameter(trigger.BackgroundID, i => i | (1 << trigger.BackgroundState));
                                    if (setOff)
                                        Background.ModifyParameter(trigger.BackgroundID, i => i & ~(1 << trigger.BackgroundState));

                                    if ((setOn || setOff) && (trigger.SoundID != 0))
                                        Game.Audio.PlaySfx(trigger.SoundID, 1f, 0f);

                                    if (active)
                                        _activeTriggers.Add(trigger);
                                    else
                                        _activeTriggers.Remove(trigger);
                                }
                            }

                            if (Options.HasFlag(FieldOptions.CameraTracksPlayer))
                                BringPlayerIntoView();

                            if ((_frame % 20) == 0) {
                                Game.SaveData.FieldDangerCounter += (int)(1024 * animSpeed * animSpeed / _encounters[BattleTable].Rate);
                                if (_r.Next(256) < (Game.SaveData.FieldDangerCounter / 256)) {
                                    System.Diagnostics.Debug.WriteLine($"FieldDangerCounter: trigger encounter and reset");
                                    Game.SaveData.FieldDangerCounter = 0;
                                    if (BattleOptions.BattlesEnabled && _encounters[BattleTable].Enabled) {
                                        Battle.BattleScreen.Launch(Game, _encounters[BattleTable], BattleOptions.Flags, _r);
                                    }
                                }
                            }

                        } else {
                            //
                        }
                    }

                    foreach (var isIn in Player.LinesCollidingWith) {
                        isIn.Call(2, 4, null); //TODO PRIORITY!?!
                    }

                    if ((Player.Model.AnimationState.Animation != desiredAnim) || (Player.Model.AnimationState.AnimationSpeed != animSpeed))
                        Player.Model.PlayAnimation(desiredAnim, true, animSpeed, null);
                }
            }
        }

        private static bool LineIntersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out float aDist) {
            double denominator = ((a1.X - a0.X) * (b1.Y - b0.Y)) - ((a1.Y - a0.Y) * (b1.X - b0.X));
            double numerator1 = 1.0 * ((a0.Y - b0.Y) * (b1.X - b0.X)) - 1.0 * ((a0.X - b0.X) * (b1.Y - b0.Y));
            double numerator2 = 1.0 * ((a0.Y - b0.Y) * (a1.X - a0.X)) - 1.0 * ((a0.X - b0.X) * (a1.Y - a0.Y));


            if (denominator == 0) {
                aDist = 0; 
                return numerator1 == 0 && numerator2 == 0;
            }

            aDist = (float)Math.Round(numerator1 / denominator, 2);
            double s = Math.Round(numerator2 / denominator, 2);

            return (aDist >= 0 && aDist <= 1) && (s >= 0 && s <= 1);
        }

        private bool CalculateTriLeave(Vector2 startPos, Vector2 endPos, WalkmeshTriangle tri, out float dist, out short? newTri, out Vector2 tv0, out Vector2 tv1) {
            tv0 = tri.V0.ToX().XY();
            tv1 = tri.V1.ToX().XY();
            if (LineIntersect(startPos, endPos, tv0, tv1, out dist)) { 
                newTri = tri.V01Tri;
                return true;
            }

            tv0 = tri.V1.ToX().XY();
            tv1 = tri.V2.ToX().XY();
            if (LineIntersect(startPos, endPos, tv0, tv1, out dist)) {
                newTri = tri.V12Tri;
                return true;
            }

            tv0 = tri.V2.ToX().XY();
            tv1 = tri.V0.ToX().XY();
            if (LineIntersect(startPos, endPos, tv0, tv1, out dist)) {
                newTri = tri.V20Tri;
                return true;
            }

            newTri = null;
            return false;
        }

        private enum LeaveTriResult {
            Failure,
            Success,
            SlideCurrentTri,
            SlideNewTri,
        }

        private void FindOtherVerts(WalkmeshTriangle tri, FieldVertex v, out FieldVertex v1, out FieldVertex v2) {
            if (tri.V0 == v) {
                v1 = tri.V1;
                v2 = tri.V2;
            } else if (tri.V1 == v) {
                v1 = tri.V0;
                v2 = tri.V2;
            } else if (tri.V2 == v) {
                v1 = tri.V0;
                v2 = tri.V1;
            } else
                throw new NotImplementedException();
        }

        private void FindAdjacentTris(WalkmeshTriangle tri, FieldVertex v, out short? t0, out short? t1) {
            if (tri.V0 == v) {
                t0 = tri.V01Tri;
                t1 = tri.V20Tri;
            } else if (tri.V1 == v) {
                t0 = tri.V01Tri;
                t1 = tri.V12Tri;
            } else if (tri.V2 == v) {
                t0 = tri.V12Tri;
                t1 = tri.V20Tri;
            } else
                throw new NotImplementedException();
        }

        private double AngleBetweenVectors(Vector2 v0, Vector2 v1) {
            double angle = Math.Atan2(v0.Y, v0.X) - Math.Atan2(v1.Y, v1.X);
            while (angle > Math.PI) angle -= 2 * Math.PI;
            while (angle <= -Math.PI) angle += 2 * Math.PI;
            return angle;
        }

        private static Random _r = new();

        private LeaveTriResult DoesLeaveTri(Vector2 startPos, Vector2 endPos, WalkmeshTriangle tri, bool allowSlide, out float dist, out short? newTri, out Vector2 newDestination) {
            newDestination = Vector2.Zero;

            var origDir = (endPos - startPos);
            var origDistance = origDir.Length();
            origDir.Normalize();

            //Now see if we're exactly on a vert. If so, find ALL the tris which join that vert.
            //We'll try and shift into one of them and then when the move is retried, we'll hopefully make some progress... :/

            foreach (var vert in tri.AllVerts()) {
                if ((vert.X == (short)startPos.X) && (vert.Y == (short)startPos.Y)) {

                    var candidates = _walkmesh
                        .SelectMany((t, index) => t.AllVerts()
                            .Where(v => v != vert)
                            .Select(otherV => {
                                var dir = otherV.ToX().XY() - vert.ToX().XY();
                                dir.Normalize();
                                return new {
                                    Tri = t,
                                    TIndex = index,
                                    VStart = vert.ToX().XY(),
                                    VEnd = otherV.ToX().XY(),
                                    Angle = AngleBetweenVectors(dir, origDir)
                                };
                            })
                        )
                        .Where(a => a.Tri.AllVerts().Any(v => v == vert))
                        .OrderBy(a => Math.Abs(a.Angle));

                    if (candidates.Any()) {
                        var choice = candidates.First();
                        if (choice.Tri != tri) {
                            dist = 0;
                            newDestination = choice.VStart;
                            newTri = (short)choice.TIndex;
                            return LeaveTriResult.SlideNewTri;
                        } else {
                            var edge = choice.VEnd - choice.VStart;
                            var distance = edge.Length();
                            edge.Normalize();
                            if (distance < origDistance)
                                newDestination = choice.VEnd;
                            else
                                newDestination = startPos + edge * origDistance;
                            newTri = null;
                            dist = 0;
                            return LeaveTriResult.SlideCurrentTri;
                        }
                    }
                }
            }


            if (!CalculateTriLeave(startPos, endPos, tri, out dist, out newTri, out Vector2 tv0, out Vector2 tv1))
                return LeaveTriResult.Failure;

            if (newTri != null)
                return LeaveTriResult.Success;

            if (allowSlide) {

                //If we get here, we're not exactly on one of the current tri's verts, but may be able
                //to slide along an edge to end up closer to our desired end point.
                //Calculate angles from end-start-v0 and end-start-v1 to find which vert we can slide towards
                //while minimising the change in direction from our original heading.
                //Only slide if the edge is < 90 degrees off our original heading as it's weird otherwise!

                var v0dir = (tv0 - startPos);
                var v0Distance = v0dir.Length();
                v0dir.Normalize();
                
                var v1dir = (tv1 - startPos);
                var v1Distance = v1dir.Length();
                v1dir.Normalize();                

                double v0angle = AngleBetweenVectors(v0dir, origDir), 
                    v1angle = AngleBetweenVectors(v1dir, origDir);

                if ((Math.Abs(v0angle) < Math.Abs(v1angle)) && (v0angle < (Math.PI / 2))) {
                    //Try to slide towards v0
                    if (v0Distance < origDistance)
                        newDestination = tv0;
                    else
                        newDestination = startPos + v0dir * origDistance;
                    return LeaveTriResult.SlideCurrentTri;
                } else if (Math.Abs(v1angle) < (Math.PI / 2)) {
                    //Try to slide towards v1
                    if (v1Distance < origDistance)
                        newDestination = tv1;
                    else
                        newDestination = startPos + v1dir * origDistance;
                    return LeaveTriResult.SlideCurrentTri;
                }

            }

            return LeaveTriResult.Failure;
        }


        public bool TryWalk(Entity eMove, Vector3 newPosition, bool doCollide) {
            //TODO: Collision detection against other models!

            if (doCollide) {
                eMove.CollidingWith.Clear();
                Entities.ForEach(e => e.CollidingWith.Remove(eMove));

                var toCheck = Entities
                    .Where(e => e.Flags.HasFlag(EntityFlags.CanCollide))
                    .Where(e => e.Model != null)
                    .Where(e => e != eMove);

                foreach (var entity in toCheck) {
                    if (entity.Model != null) {
                        var dist = (entity.Model.Translation.XY() - newPosition.XY()).Length();
                        var collision = eMove.CollideDistance + entity.CollideDistance;
                        if (dist <= collision) {
                            System.Diagnostics.Debug.WriteLine($"Entity {eMove} is now colliding with {entity}");
                            eMove.CollidingWith.Add(entity);
                            entity.CollidingWith.Add(eMove);
                        }
                    }
                }
                if (eMove.CollidingWith.Any())
                    return false;
            }

            var currentTri = _walkmesh[eMove.WalkmeshTri];
            var newHeight = HeightInTriangle(currentTri.V0.ToX(), currentTri.V1.ToX(), currentTri.V2.ToX(), newPosition.X, newPosition.Y);
            if (newHeight != null) {
                //We're staying in the same tri, so just update height
                eMove.Model.Translation = newPosition.WithZ(newHeight.Value);
                return true;
            } else {
                switch (DoesLeaveTri(eMove.Model.Translation.XY(), newPosition.XY(), currentTri, true, out float dist, out short? newTri, out Vector2 newDest)) {
                    case LeaveTriResult.Failure:
                        System.Diagnostics.Debug.WriteLine($"Moving from {eMove.Model.Translation} to {newPosition}");
                        System.Diagnostics.Debug.WriteLine($"V0 {currentTri.V0}, V1 {currentTri.V1}, V2 {currentTri.V2}");
                        throw new Exception($"Reality failure: Can't find route out of triangle");
                    case LeaveTriResult.Success:
                        break; //Woo
                    case LeaveTriResult.SlideCurrentTri:
                        newHeight = HeightInTriangle(currentTri.V0.ToX(), currentTri.V1.ToX(), currentTri.V2.ToX(), newDest.X, newDest.Y);
                        if (newHeight == null)
                            throw new Exception();
                        eMove.Model.Translation = new Vector3(newDest.X, newDest.Y, newHeight.Value);
                        return true;
                    case LeaveTriResult.SlideNewTri:
                        newPosition = new Vector3(newDest, 0);
                        break; //Treat same as success, code below will move us into the new tri
                    default:
                        throw new NotImplementedException();
                }

                if ((newTri == null) || DisabledWalkmeshTriangles.Contains(newTri.Value)) {
                    //Just can't leave by this side, oh well
                    return false;
                } else {
                    var movingToTri = _walkmesh[newTri.Value];
                    newHeight = HeightInTriangle(
                        movingToTri.V0.ToX(), movingToTri.V1.ToX(), movingToTri.V2.ToX(),
                        newPosition.X, newPosition.Y
                    );
                    Vector2 testLocation = eMove.Model.Translation.XY();
                    while (newHeight == null) {
                        //Our destination triangle isn't directly connected to our start location -
                        //we need to loop through and check all the intermediate points are within
                        //(walkable) triangles
                        var vector = newPosition.XY() - testLocation;
                        var testFrom = testLocation + vector * (dist * 1.05f);
                        switch(DoesLeaveTri(testFrom, newPosition.XY(), movingToTri, false, out dist, out newTri, out newDest)) {
                            case LeaveTriResult.Failure:
                                throw new Exception($"Reality failure: Can't find route out of triangle");
                        }
                        if ((newTri == null) || DisabledWalkmeshTriangles.Contains(newTri.Value)) {
                            //Just can't leave by this side, oh well
                            return false;
                        }
                        testLocation = testFrom;

                        movingToTri = _walkmesh[newTri.Value];
                        newHeight = HeightInTriangle(
                            movingToTri.V0.ToX(), movingToTri.V1.ToX(), movingToTri.V2.ToX(),
                            newPosition.X, newPosition.Y
                        );
                    }

                    eMove.WalkmeshTri = newTri.Value;
                    eMove.Model.Translation = newPosition.WithZ(newHeight.Value);
                    return true;                    
                }
            }
        }

        private static float? HeightInTriangle(Vector3 p0, Vector3 p1, Vector3 p2, float x, float y) {
            double denominator = (p1.Y - p2.Y) * (p0.X - p2.X) + (p2.X - p1.X) * (p0.Y - p2.Y);
            var a = ((p1.Y - p2.Y) * (x - p2.X) + (p2.X - p1.X) * (y - p2.Y)) / denominator;
            var b = ((p2.Y - p0.Y) * (x - p2.X) + (p0.X - p2.X) * (y - p2.Y)) / denominator;

            a = Math.Round(a, 4);
            b = Math.Round(b, 4);
            var c = Math.Round(1 - a - b, 4);

            if (a < 0) return null;
            if (b < 0) return null;
            if (c < 0) return null;
            if (a > 1) return null;
            if (b > 1) return null;
            if (c > 1) return null;

            return (float)(p0.Z * a + p1.Z * b + p2.Z * c);
        }

        public void DropToWalkmesh(Entity e, Vector2 position, int walkmeshTri) {
            var tri = _walkmesh[walkmeshTri];

            e.Model.Translation = new Vector3(
                position.X,
                position.Y,
                HeightInTriangle(tri.V0.ToX(), tri.V1.ToX(), tri.V2.ToX(), position.X, position.Y).Value
            );
            e.WalkmeshTri = walkmeshTri;
        }

        public void CheckPendingPlayerSetup() {
            if ((_destination != null) && (Player.Model != null)) {
                DropToWalkmesh(Player, new Vector2(_destination.X, _destination.Y), _destination.Triangle);
                Player.Model.Rotation = new Vector3(0, 0, 360f * _destination.Orientation / 255f);
                _destination = null;
            }
        }

        public void SetPlayer(int whichEntity) {
            Player = Entities[whichEntity]; //TODO: also center screen etc.
            //TODO - is this reasonable...? Probably?!
            if (Player.CollideDistance == 0)
                Player.CollideDistance = 20;
            CheckPendingPlayerSetup();
            WhenPlayerSet?.Invoke();
        }

        public void SetPlayerControls(bool enabled) {
            if (enabled)
                Options |= FieldOptions.PlayerControls | FieldOptions.CameraTracksPlayer; //Seems like cameratracksplayer MUST be turned on now or things break...?
            else {
                Options &= ~FieldOptions.PlayerControls;
                if (Player?.Model != null)
                    Player.Model.PlayAnimation(0, true, 1f, null);
                //TODO - is this reasonable? Disable current (walking) animation when we take control away from the player? 
                //(We don't want e.g. walk animation to be continuing after our control is disabled and we're not moving any more!)
            }
        }

        public void TriggerBattle(int which) {
            Battle.BattleScreen.Launch(Game, which, BattleOptions.Flags);
        }


        public void Received(Net.FieldModelMessage message) {
            var model = FieldModels[message.ModelID];
            if (message.Visible.HasValue)
                model.Visible = message.Visible.Value;
            if (message.Translation.HasValue)
                model.Translation = message.Translation.Value;
            if (message.Translation2.HasValue)
                model.Translation2 = message.Translation2.Value;
            if (message.Rotation.HasValue)
                model.Rotation = message.Rotation.Value;
            if (message.Rotation2.HasValue)
                model.Rotation2 = message.Rotation2.Value;
            if (message.Scale.HasValue)
                model.Scale = message.Scale.Value;
            if (message.AnimationState != null)
                model.AnimationState = message.AnimationState;
        }

        public void Received(Net.FieldBGMessage message) {
            Background.SetParameter(message.Parm, message.Value);
        }
        public void Received(Net.FieldBGScrollMessage message) {
            BGScroll(message.X, message.Y);
        }

        public void Received(Net.FieldEntityModelMessage message) {
            Entities[message.EntityID].Model = FieldModels[message.ModelID];
        }
    }

    public class CachedField {
        public int FieldID { get; set; } = -1;
        public FieldFile FieldFile { get; set; }

        public void Load(FGame g, int fieldID) {
            var mapList = g.Singleton(() => new MapList(g.Open("field", "maplist")));
            string file = mapList.Items[fieldID];
            using (var s = g.Open("field", file))
                FieldFile = new FieldFile(s);
            FieldID = fieldID;
        }
    }
}
