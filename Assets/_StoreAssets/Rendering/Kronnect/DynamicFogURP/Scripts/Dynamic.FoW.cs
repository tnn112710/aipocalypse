//------------------------------------------------------------------------------------------------------------------
// Dynamic Fog & Mist 2
// Created by Kronnect
//------------------------------------------------------------------------------------------------------------------
using UnityEngine;
using System.Collections.Generic;


namespace DynamicFogAndMist2 {


    public enum MASK_TEXTURE_BRUSH_MODE {
        AddFog = 0,
        RemoveFog = 1,
        ColorFog = 2
    }


    public partial class DynamicFog : MonoBehaviour {

        public bool enableFogOfWar;
        public bool fogOfWarShowBoundaries;
        public Vector3 fogOfWarCenter;
        public Vector3 fogOfWarSize = new Vector3(1024, 0, 1024);
        [Range(32, 2048)] public int fogOfWarTextureSize = 256;
        [Range(0, 100)] public float fogOfWarRestoreDelay;
        [Range(0, 25)] public float fogOfWarRestoreDuration = 2f;
        [Range(0, 1)] public float fogOfWarSmoothness = 1f;
        public bool fogOfWarBlur;

        const int INITIAL_TRANSITIONS_CAPACITY = 256;
        const int MAX_RETAINED_TRANSITIONS = 4096;
        bool canDestroyFOWTexture;

        #region In-Editor fog of war painter

        public bool maskEditorEnabled;
        public bool maskEditorDimFog;
        public MASK_TEXTURE_BRUSH_MODE maskBrushMode = MASK_TEXTURE_BRUSH_MODE.RemoveFog;
        public Color maskBrushColor = Color.white;
        [Range(1, 128)] public int maskBrushWidth = 20;
        [Range(0, 1)] public float maskBrushFuzziness = 0.5f;
        [Range(0, 1)] public float maskBrushOpacity = 0.15f;

        #endregion

        [SerializeField]
        Texture2D _fogOfWarTexture;

        public Texture2D fogOfWarTexture {
            get { return _fogOfWarTexture; }
            set {
                if (_fogOfWarTexture != value) {
                    if (value != null) {
                        if (value.width != value.height) {
                            Debug.LogError("Fog of war texture must be square.");
                        }
                        else if (!value.isReadable) {
                            Debug.LogError("Fog of war texture must be readable (enable Read/Write in the texture import settings).");
                        }
                        else {
                            _fogOfWarTexture = value;
                            canDestroyFOWTexture = false;
                            ReloadFogOfWarTexture();
                        }
                    }
                }
            }
        }


        Color32[] fogOfWarColorBuffer;

        struct FogOfWarTransition {
            public int x, y;
            public float startTime, startDelay;
            public float duration;
            public int initialAlpha;
            public int targetAlpha;
            public float restoreDelay, restoreDuration;
        }

        FogOfWarTransition[] fowTransitionList;
        int lastTransitionPos;
        Dictionary<int, int> fowTransitionIndices;
        bool requiresTextureUpload;
        Material fowBlur;
        RenderTexture fowBlur1, fowBlur2;


        void FogOfWarInit () {
            EnsureTransitionStructures();
            ClearTransitions();

            if (_fogOfWarTexture == null) {
                FogOfWarUpdateTexture();
            }
            else if (enableFogOfWar && (fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length == 0)) {
                ReloadFogOfWarTexture();
            }
        }

        void FogOfWarDestroy () {
            if (canDestroyFOWTexture) {
                DestroyImmediate(_fogOfWarTexture);
            }
            if (fowBlur1 != null) {
                fowBlur1.Release();
            }
            if (fowBlur2 != null) {
                fowBlur2.Release();
            }
        }

        void EnsureTransitionStructures () {
            if (fowTransitionList == null) {
                fowTransitionList = new FogOfWarTransition[INITIAL_TRANSITIONS_CAPACITY];
            }
            if (fowTransitionIndices == null) {
                fowTransitionIndices = new Dictionary<int, int>();
            }
        }

        void ClearTransitions () {
            lastTransitionPos = -1;
            fowTransitionIndices.Clear();
            ReleaseTransitionCapacityIfIdle();
        }

        // Retention policy: a large pool is dropped once no transition is active
        void ReleaseTransitionCapacityIfIdle () {
            if (lastTransitionPos < 0 && fowTransitionList.Length > MAX_RETAINED_TRANSITIONS) {
                fowTransitionList = new FogOfWarTransition[INITIAL_TRANSITIONS_CAPACITY];
            }
        }

        /// <summary>
        /// Reloads the current contents of the fog of war texture
        /// </summary>
        public void ReloadFogOfWarTexture () {
            if (_fogOfWarTexture == null) return;
            EnsureTransitionStructures();
            fogOfWarTextureSize = _fogOfWarTexture.width;
            fogOfWarColorBuffer = _fogOfWarTexture.GetPixels32();
            ClearTransitions();
            enableFogOfWar = true;
            BindFogOfWarTexture();
        }

        void BindFogOfWarTexture () {
            if (fogMat == null || _fogOfWarTexture == null) return;
            fogMat.SetTexture(ShaderParams.FogOfWarTex, _fogOfWarTexture);
        }

        // Runtime mask availability
        bool EnsureFogOfWarTexture () {
            if (_fogOfWarTexture == null) {
                FogOfWarUpdateTexture();
                if (_fogOfWarTexture == null) return false;
                BindFogOfWarTexture();
            }
            EnsureTransitionStructures();
            return fogOfWarColorBuffer != null && fogOfWarColorBuffer.Length == _fogOfWarTexture.width * _fogOfWarTexture.height;
        }


        void FogOfWarUpdateTexture () {
            if (!enableFogOfWar || !Application.isPlaying)
                return;
            int size = GetScaledSize(fogOfWarTextureSize, 1.0f);
            if (_fogOfWarTexture == null || _fogOfWarTexture.width != size || _fogOfWarTexture.height != size) {
                _fogOfWarTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                _fogOfWarTexture.hideFlags = HideFlags.DontSave;
                _fogOfWarTexture.filterMode = FilterMode.Bilinear;
                _fogOfWarTexture.wrapMode = TextureWrapMode.Clamp;
                canDestroyFOWTexture = true;
                ResetFogOfWar();
            }
        }

        int GetScaledSize (int size, float factor) {
            size = (int)(size / factor);
            size /= 4;
            if (size < 1)
                size = 1;
            return size * 4;
        }



        /// <summary>
        /// Updates fog of war transitions and uploads texture changes to GPU if required
        /// </summary>
        public void UpdateFogOfWar (bool forceUpload = false) {
            if (!enableFogOfWar)
                return;

            if (!EnsureFogOfWarTexture())
                return;

            if (forceUpload) {
                requiresTextureUpload = true;
            }

            int tw = _fogOfWarTexture.width;
            float now = Time.time;
            for (int k = 0; k <= lastTransitionPos; k++) {
                FogOfWarTransition fw = fowTransitionList[k];
                float elapsed = now - fw.startTime - fw.startDelay;
                if (elapsed <= 0)
                    continue;
                float t = fw.duration <= 0 ? 1 : elapsed / fw.duration;
                if (t < 0) t = 0; else if (t > 1f) t = 1f;
                int alpha = (int)(fw.initialAlpha + (fw.targetAlpha - fw.initialAlpha) * t);
                int colorPos = fw.y * tw + fw.x;
                fogOfWarColorBuffer[colorPos].a = (byte)alpha;
                requiresTextureUpload = true;
                if (t >= 1f) {
                    ReleaseTransitionSlot(k);
                    k--;
                    // Add refill slot if needed
                    if (fw.targetAlpha < 255 && fw.restoreDelay > 0) {
                        AddFogOfWarTransitionSlot(fw.x, fw.y, (byte)fw.targetAlpha, 255, fw.restoreDelay, fw.restoreDuration, 0, 0);
                    }
                }
            }
            if (requiresTextureUpload) {
                _fogOfWarTexture.SetPixels32(fogOfWarColorBuffer);
                _fogOfWarTexture.Apply();

#if UNITY_EDITOR
                if (!Application.isPlaying) {
                    UnityEditor.EditorUtility.SetDirty(_fogOfWarTexture);
                }
#endif

            }

            // Smooth texture
            if (fogOfWarBlur) {
                SetFowBlurTexture();
            }

            requiresTextureUpload = false;

        }

        void SetFowBlurTexture () {
            if (fowBlur == null) {
                fowBlur = new Material(Shader.Find("DynamicFog2/FoWBlur"));
                fowBlur.hideFlags = HideFlags.DontSave;
            }
            if (fowBlur == null)
                return;

            if (fowBlur1 == null || fowBlur1.width != _fogOfWarTexture.width || fowBlur2 == null || fowBlur2.width != _fogOfWarTexture.width) {
                CreateFoWBlurRTs();
            }
            fowBlur1.DiscardContents();
            Graphics.Blit(_fogOfWarTexture, fowBlur1, fowBlur, 0);
            fowBlur2.DiscardContents();
            Graphics.Blit(fowBlur1, fowBlur2, fowBlur, 1);
            fogMat.SetTexture(ShaderParams.FogOfWarTex, fowBlur2);
        }


        void CreateFoWBlurRTs () {
            if (fowBlur1 != null) {
                fowBlur1.Release();
            }
            if (fowBlur2 != null) {
                fowBlur2.Release();
            }
            RenderTextureDescriptor desc = new RenderTextureDescriptor(_fogOfWarTexture.width, _fogOfWarTexture.height, RenderTextureFormat.ARGB32, 0);
            fowBlur1 = new RenderTexture(desc);
            fowBlur2 = new RenderTexture(desc);
        }

        /// <summary>
        /// Changes the alpha value of the fog of war at world position creating a 1 second transition from current alpha value to specified target alpha. It takes into account FogOfWarCenter and FogOfWarSize.
        /// Note that only x and z coordinates are used. Y (vertical) coordinate is ignored.
        /// </summary>
        /// <param name="worldPosition">in world space coordinates.</param>
        /// <param name="radius">radius of application in world units.</param>
        public void SetFogOfWarAlpha (Vector3 worldPosition, float radius, float fogNewAlpha) {
            SetFogOfWarAlpha(worldPosition, radius, fogNewAlpha, 1f);
        }


        /// <summary>
        /// Changes the alpha value of the fog of war at world position creating a transition from current alpha value to specified target alpha. It takes into account FogOfWarCenter and FogOfWarSize.
        /// Note that only x and z coordinates are used. Y (vertical) coordinate is ignored.
        /// </summary>
        /// <param name="worldPosition">in world space coordinates.</param>
        /// <param name="radius">radius of application in world units.</param>
        /// <param name="fogNewAlpha">target alpha value.</param>
        /// <param name="duration">duration of transition in seconds (0 = apply fogNewAlpha instantly).</param>
        public void SetFogOfWarAlpha (Vector3 worldPosition, float radius, float fogNewAlpha, float duration) {
            SetFogOfWarAlpha(worldPosition, radius, fogNewAlpha, true, duration, fogOfWarSmoothness, fogOfWarRestoreDelay, fogOfWarRestoreDuration);
        }


        /// <summary>
        /// Changes the alpha value of the fog of war at world position creating a transition from current alpha value to specified target alpha. It takes into account FogOfWarCenter and FogOfWarSize.
        /// Note that only x and z coordinates are used. Y (vertical) coordinate is ignored.
        /// </summary>
        /// <param name="worldPosition">in world space coordinates.</param>
        /// <param name="radius">radius of application in world units.</param>
        /// <param name="fogNewAlpha">target alpha value.</param>
        /// <param name="duration">duration of transition in seconds (0 = apply fogNewAlpha instantly).</param>
        /// <param name="smoothness">border smoothness.</param>
        public void SetFogOfWarAlpha (Vector3 worldPosition, float radius, float fogNewAlpha, float duration, float smoothness) {
            SetFogOfWarAlpha(worldPosition, radius, fogNewAlpha, true, duration, smoothness, fogOfWarRestoreDelay, fogOfWarRestoreDuration);
        }

        /// <summary>
        /// Changes the alpha value of the fog of war at world position creating a transition from current alpha value to specified target alpha. It takes into account FogOfWarCenter and FogOfWarSize.
        /// Note that only x and z coordinates are used. Y (vertical) coordinate is ignored.
        /// </summary>
        /// <param name="worldPosition">in world space coordinates.</param>
        /// <param name="radius">radius of application in world units.</param>
        /// <param name="fogNewAlpha">target alpha value.</param>
        /// <param name="blendAlpha">if new alpha is combined with preexisting alpha value or replaced.</param>
        /// <param name="duration">duration of transition in seconds (0 = apply fogNewAlpha instantly).</param>
        /// <param name="smoothness">border smoothness.</param>
        /// <param name="restoreDelay">delay before the fog alpha is restored. Pass 0 to keep change forever.</param>
        /// <param name="restoreDuration">restore duration in seconds.</param>
        public void SetFogOfWarAlpha (Vector3 worldPosition, float radius, float fogNewAlpha, bool blendAlpha, float duration, float smoothness, float restoreDelay, float restoreDuration) {
            if (!EnsureFogOfWarTexture())
                return;

            float tx = (worldPosition.x - fogOfWarCenter.x) / fogOfWarSize.x + 0.5f;
            if (tx < 0 || tx > 1f)
                return;
            float tz = (worldPosition.z - fogOfWarCenter.z) / fogOfWarSize.z + 0.5f;
            if (tz < 0 || tz > 1f)
                return;

            int tw = _fogOfWarTexture.width;
            int th = _fogOfWarTexture.height;
            int px = (int)(tx * tw);
            int pz = (int)(tz * th);
            float sm = 0.0001f + smoothness;
            byte newAlpha8 = (byte)(fogNewAlpha * 255);
            int deltax = (int)(tw * radius / fogOfWarSize.x);
            int deltaz = (int)(th * radius / fogOfWarSize.z);
            float invDeltax = 1f / Mathf.Max(1, deltax);
            float invDeltaz = 1f / Mathf.Max(1, deltaz);
            for (int r = pz - deltaz; r <= pz + deltaz; r++) {
                if (r <= 0 || r >= th - 1) continue;
                float nz = (pz - r) * invDeltaz;
                for (int c = px - deltax; c <= px + deltax; c++) {
                    if (c <= 0 || c >= tw - 1) continue;
                    float nx = (px - c) * invDeltax;
                    float distanceSqr = nx * nx + nz * nz;
                    if (distanceSqr > 1f) continue;
                    int colorBufferPos = r * tw + c;
                    byte storedAlpha = fogOfWarColorBuffer[colorBufferPos].a;
                    int refAlpha = blendAlpha ? storedAlpha : 255;
                    float t = 1f - (1f - distanceSqr) / sm;
                    if (t < 0) t = 0; else if (t > 1f) t = 1f;
                    byte targetAlpha = (byte)(newAlpha8 + (refAlpha - newAlpha8) * t);
                    ApplyFogOfWarPixel(c, r, colorBufferPos, storedAlpha, targetAlpha, duration, restoreDelay, restoreDuration);
                }
            }
        }

        /// <summary>
        /// Changes the alpha value of the fog of war within bounds creating a transition from current alpha value to specified target alpha. It takes into account FogOfWarCenter and FogOfWarSize.
        /// Note that only x and z coordinates are used. Y (vertical) coordinate is ignored.
        /// </summary>
        /// <param name="bounds">in world space coordinates.</param>
        /// <param name="fogNewAlpha">target alpha value.</param>
        /// <param name="duration">duration of transition in seconds (0 = apply fogNewAlpha instantly).</param>
        public void SetFogOfWarAlpha (Bounds bounds, float fogNewAlpha, float duration) {
            SetFogOfWarAlpha(bounds, fogNewAlpha, true, duration, fogOfWarSmoothness, fogOfWarRestoreDelay, fogOfWarRestoreDuration);
        }

        /// <summary>
        /// Changes the alpha value of the fog of war within bounds creating a transition from current alpha value to specified target alpha. It takes into account FogOfWarCenter and FogOfWarSize.
        /// Note that only x and z coordinates are used. Y (vertical) coordinate is ignored.
        /// </summary>
        /// <param name="bounds">in world space coordinates.</param>
        /// <param name="fogNewAlpha">target alpha value.</param>
        /// <param name="duration">duration of transition in seconds (0 = apply fogNewAlpha instantly).</param>
        /// <param name="smoothness">border smoothness.</param>
        public void SetFogOfWarAlpha (Bounds bounds, float fogNewAlpha, float duration, float smoothness) {
            SetFogOfWarAlpha(bounds, fogNewAlpha, true, duration, smoothness, fogOfWarRestoreDelay, fogOfWarRestoreDuration);
        }


        /// <summary>
        /// Changes the alpha value of the fog of war within bounds creating a transition from current alpha value to specified target alpha. It takes into account FogOfWarCenter and FogOfWarSize.
        /// Note that only x and z coordinates are used. Y (vertical) coordinate is ignored.
        /// </summary>
        /// <param name="bounds">in world space coordinates.</param>
        /// <param name="fogNewAlpha">target alpha value.</param>
        /// <param name="blendAlpha">if new alpha is combined with preexisting alpha value or replaced.</param>
        /// <param name="duration">duration of transition in seconds (0 = apply fogNewAlpha instantly).</param>
        /// <param name="smoothness">border smoothness.</param>
        /// <param name="restoreDelay">delay before the fog alpha is restored. Pass 0 to keep change forever.</param>
        /// <param name="restoreDuration">restore duration in seconds.</param>
        public void SetFogOfWarAlpha (Bounds bounds, float fogNewAlpha, bool blendAlpha, float duration, float smoothness, float restoreDelay, float restoreDuration) {
            if (!EnsureFogOfWarTexture())
                return;

            Vector3 worldPosition = bounds.center;
            float tx = (worldPosition.x - fogOfWarCenter.x) / fogOfWarSize.x + 0.5f;
            if (tx < 0 || tx > 1f)
                return;
            float tz = (worldPosition.z - fogOfWarCenter.z) / fogOfWarSize.z + 0.5f;
            if (tz < 0 || tz > 1f)
                return;

            int tw = _fogOfWarTexture.width;
            int th = _fogOfWarTexture.height;
            int px = (int)(tx * tw);
            int pz = (int)(tz * th);
            byte newAlpha8 = (byte)(fogNewAlpha * 255);
            float trz = Mathf.Max(1e-6f, bounds.extents.z / fogOfWarSize.z);
            float trx = Mathf.Max(1e-6f, bounds.extents.x / fogOfWarSize.x);
            float aspect1 = trx > trz ? 1f : trz / trx;
            float aspect2 = trx > trz ? trx / trz : 1f;
            int deltaz = (int)(th * trz);
            int deltax = (int)(tw * trx);
            float deltazSqr = Mathf.Max(1, deltaz * deltaz);
            float deltaxSqr = Mathf.Max(1, deltax * deltax);
            float sm = 0.0001f + smoothness;
            for (int r = pz - deltaz; r <= pz + deltaz; r++) {
                if (r <= 0 || r >= th - 1) continue;
                float distancezSqr = deltazSqr - (pz - r) * (pz - r);
                float t1 = distancezSqr * aspect1 / (deltazSqr * sm);
                for (int c = px - deltax; c <= px + deltax; c++) {
                    if (c <= 0 || c >= tw - 1) continue;
                    int colorBufferPos = r * tw + c;
                    byte storedAlpha = fogOfWarColorBuffer[colorBufferPos].a;
                    int refAlpha = blendAlpha ? storedAlpha : 255;
                    float distancexSqr = deltaxSqr - (px - c) * (px - c);
                    float t2 = distancexSqr * aspect2 / (deltaxSqr * sm);
                    float t = t1 < t2 ? t1 : t2;
                    t = 1f - t;
                    if (t < 0) t = 0; else if (t > 1f) t = 1f;
                    byte targetAlpha = (byte)(newAlpha8 + (refAlpha - newAlpha8) * t);
                    ApplyFogOfWarPixel(c, r, colorBufferPos, storedAlpha, targetAlpha, duration, restoreDelay, restoreDuration);
                }
            }
        }

        void ApplyFogOfWarPixel (int x, int y, int colorBufferPos, byte storedAlpha, byte targetAlpha, float duration, float restoreDelay, float restoreDuration) {
            bool needsRestore = restoreDelay > 0 && targetAlpha < 255;
            if (storedAlpha == targetAlpha && !needsRestore) {
                if (fowTransitionIndices.Count > 0) {
                    CancelFogOfWarTransition(x, y);
                }
                return;
            }
            if (duration > 0) {
                AddFogOfWarTransitionSlot(x, y, storedAlpha, targetAlpha, 0, duration, restoreDelay, restoreDuration);
            }
            else {
                CancelFogOfWarTransition(x, y);
                Color32 colorBuffer = fogOfWarColorBuffer[colorBufferPos];
                colorBuffer.a = targetAlpha;
                fogOfWarColorBuffer[colorBufferPos] = colorBuffer;
                requiresTextureUpload = true;
                if (needsRestore) {
                    AddFogOfWarTransitionSlot(x, y, targetAlpha, 255, restoreDelay, restoreDuration, 0, 0);
                }
            }
        }

        /// <summary>
        /// Restores fog of war to full opacity
        /// </summary>
        /// <param name="worldPosition">World position.</param>
        /// <param name="radius">Radius.</param>
        public void ResetFogOfWarAlpha (Vector3 worldPosition, float radius) {
            if (!EnsureFogOfWarTexture())
                return;

            float tx = (worldPosition.x - fogOfWarCenter.x) / fogOfWarSize.x + 0.5f;
            if (tx < 0 || tx > 1f)
                return;
            float tz = (worldPosition.z - fogOfWarCenter.z) / fogOfWarSize.z + 0.5f;
            if (tz < 0 || tz > 1f)
                return;

            int tw = _fogOfWarTexture.width;
            int th = _fogOfWarTexture.height;
            int px = (int)(tx * tw);
            int pz = (int)(tz * th);
            int deltax = (int)(tw * radius / fogOfWarSize.x);
            int deltaz = (int)(th * radius / fogOfWarSize.z);
            float invDeltax = 1f / Mathf.Max(1, deltax);
            float invDeltaz = 1f / Mathf.Max(1, deltaz);
            for (int r = pz - deltaz; r <= pz + deltaz; r++) {
                if (r <= 0 || r >= th - 1) continue;
                float nz = (pz - r) * invDeltaz;
                for (int c = px - deltax; c <= px + deltax; c++) {
                    if (c <= 0 || c >= tw - 1) continue;
                    float nx = (px - c) * invDeltax;
                    if (nx * nx + nz * nz > 1f) continue;
                    ResetFogOfWarPixel(c, r, r * tw + c);
                }
            }
        }



        /// <summary>
        /// Restores fog of war to full opacity
        /// </summary>
        public void ResetFogOfWarAlpha (Bounds bounds) {
            ResetFogOfWarAlpha(bounds.center, bounds.extents.x, bounds.extents.z);
        }

        /// <summary>
        /// Restores fog of war to full opacity
        /// </summary>
        public void ResetFogOfWarAlpha (Vector3 position, Vector3 size) {
            ResetFogOfWarAlpha(position, size.x * 0.5f, size.z * 0.5f);
        }

        /// <summary>
        /// Restores fog of war to full opacity
        /// </summary>
        /// <param name="position">Position in world space.</param>
        /// <param name="extentsX">Half of the length of the rectangle in X-Axis.</param>
        /// <param name="extentsZ">Half of the length of the rectangle in Z-Axis.</param>
        public void ResetFogOfWarAlpha (Vector3 position, float extentsX, float extentsZ) {
            if (!EnsureFogOfWarTexture())
                return;

            float tx = (position.x - fogOfWarCenter.x) / fogOfWarSize.x + 0.5f;
            if (tx < 0 || tx > 1f)
                return;
            float tz = (position.z - fogOfWarCenter.z) / fogOfWarSize.z + 0.5f;
            if (tz < 0 || tz > 1f)
                return;

            int tw = _fogOfWarTexture.width;
            int th = _fogOfWarTexture.height;
            int px = (int)(tx * tw);
            int pz = (int)(tz * th);
            int deltaz = (int)(th * extentsZ / fogOfWarSize.z);
            int deltax = (int)(tw * extentsX / fogOfWarSize.x);
            for (int r = pz - deltaz; r <= pz + deltaz; r++) {
                if (r <= 0 || r >= th - 1) continue;
                for (int c = px - deltax; c <= px + deltax; c++) {
                    if (c <= 0 || c >= tw - 1) continue;
                    ResetFogOfWarPixel(c, r, r * tw + c);
                }
            }
        }

        void ResetFogOfWarPixel (int x, int y, int colorBufferPos) {
            CancelFogOfWarTransition(x, y);
            Color32 colorBuffer = fogOfWarColorBuffer[colorBufferPos];
            if (colorBuffer.a == 255) return;
            colorBuffer.a = 255;
            fogOfWarColorBuffer[colorBufferPos] = colorBuffer;
            requiresTextureUpload = true;
        }


        public void ResetFogOfWar (byte alpha = 255) {
            if (_fogOfWarTexture == null)
                return;
            EnsureTransitionStructures();
            int h = _fogOfWarTexture.height;
            int w = _fogOfWarTexture.width;
            int newLength = h * w;
            if (fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length != newLength) {
                fogOfWarColorBuffer = new Color32[newLength];
            }
            Color32 opaque = new Color32(255, 255, 255, alpha);
            for (int k = 0; k < newLength; k++) {
                fogOfWarColorBuffer[k] = opaque;
            }
            _fogOfWarTexture.SetPixels32(fogOfWarColorBuffer);
            _fogOfWarTexture.Apply();
            ClearTransitions();
        }

        /// <summary>
        /// Gets or set fog of war state as a Color32 buffer. The alpha channel stores the transparency of the fog at that position (0 = no fog, 1 = opaque).
        /// </summary>
        public Color32[] fogOfWarTextureData {
            get {
                return fogOfWarColorBuffer;
            }
            set {
                if (value == null) {
                    Debug.LogError("Fog of war texture data cannot be null.");
                    return;
                }
                enableFogOfWar = true;
                if (_fogOfWarTexture == null) {
                    FogOfWarUpdateTexture();
                    if (_fogOfWarTexture == null) return;
                    BindFogOfWarTexture();
                }
                if (value.Length != _fogOfWarTexture.width * _fogOfWarTexture.height) {
                    Debug.LogError("Fog of war texture data length does not match the fog of war texture size.");
                    return;
                }
                EnsureTransitionStructures();
                fogOfWarColorBuffer = value;
                ClearTransitions();
                _fogOfWarTexture.SetPixels32(fogOfWarColorBuffer);
                _fogOfWarTexture.Apply();
            }
        }

        static int TransitionKey (int x, int y) {
            return (y << 16) | x;
        }

        // Dense list: the last active transition takes the released slot
        void ReleaseTransitionSlot (int index) {
            int last = lastTransitionPos;
            fowTransitionIndices.Remove(TransitionKey(fowTransitionList[index].x, fowTransitionList[index].y));
            if (index != last) {
                fowTransitionList[index] = fowTransitionList[last];
                fowTransitionIndices[TransitionKey(fowTransitionList[index].x, fowTransitionList[index].y)] = index;
            }
            lastTransitionPos = last - 1;
            ReleaseTransitionCapacityIfIdle();
        }

        void CancelFogOfWarTransition (int x, int y) {
            int index;
            if (fowTransitionIndices.TryGetValue(TransitionKey(x, y), out index)) {
                ReleaseTransitionSlot(index);
            }
        }

        void AddFogOfWarTransitionSlot (int x, int y, byte initialAlpha, byte targetAlpha, float delay, float duration, float restoreDelay, float restoreDuration) {

            int key = TransitionKey(x, y);
            int index;
            if (!fowTransitionIndices.TryGetValue(key, out index)) {
                if (lastTransitionPos >= fowTransitionList.Length - 1) {
                    int maxTransitions = _fogOfWarTexture.width * _fogOfWarTexture.height;
                    if (fowTransitionList.Length >= maxTransitions)
                        return;
                    System.Array.Resize(ref fowTransitionList, Mathf.Min(maxTransitions, fowTransitionList.Length * 2));
                }
                index = ++lastTransitionPos;
                fowTransitionIndices[key] = index;
            }

            fowTransitionList[index].x = x;
            fowTransitionList[index].y = y;
            fowTransitionList[index].duration = duration;
            fowTransitionList[index].startTime = Time.time;
            fowTransitionList[index].startDelay = delay;
            fowTransitionList[index].initialAlpha = initialAlpha;
            fowTransitionList[index].targetAlpha = targetAlpha;
            fowTransitionList[index].restoreDelay = restoreDelay;
            fowTransitionList[index].restoreDuration = restoreDuration;
        }


        /// <summary>
        /// Gets the current alpha value of the Fog of War at a given world position
        /// </summary>
        /// <returns>The fog of war alpha.</returns>
        /// <param name="worldPosition">World position.</param>
        public float GetFogOfWarAlpha (Vector3 worldPosition) {
            if (fogOfWarColorBuffer == null || fogOfWarColorBuffer.Length == 0 || _fogOfWarTexture == null)
                return 1f;

            float tx = (worldPosition.x - fogOfWarCenter.x) / fogOfWarSize.x + 0.5f;
            if (tx < 0 || tx > 1f)
                return 1f;
            float tz = (worldPosition.z - fogOfWarCenter.z) / fogOfWarSize.z + 0.5f;
            if (tz < 0 || tz > 1f)
                return 1f;

            int tw = _fogOfWarTexture.width;
            int th = _fogOfWarTexture.height;
            int px = Mathf.Min(tw - 1, (int)(tx * tw));
            int pz = Mathf.Min(th - 1, (int)(tz * th));
            int colorBufferPos = pz * tw + px;
            if (colorBufferPos < 0 || colorBufferPos >= fogOfWarColorBuffer.Length)
                return 1f;
            return fogOfWarColorBuffer[colorBufferPos].a / 255f;
        }

    }


}
