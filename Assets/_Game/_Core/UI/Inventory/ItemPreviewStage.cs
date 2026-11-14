using DG.Tweening;
using MB13.Aipocalypse.Core.EquipmentSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // The inventory inspector's 3D view: the selected item's world model (a gun with its parts), lit and rendered by its
    // own camera into the RawImage, far below the world on its own layer. Built when first shown; the camera renders only
    // while something is shown. Turn by dragging, zoom with the wheel (ItemPreviewInput).
    public sealed class ItemPreviewStage : MonoBehaviour
    {
        [SerializeField] RawImage _image;
        [SerializeField, Tooltip("World position of the stage, far from anything else")] Vector3 _position = new(0f, -2000f, 0f);
        [SerializeField, Tooltip("Layer the stage's models are drawn on; only the stage camera sees it")] string _layer = "ItemPreview";
        [SerializeField, Tooltip("Index in the pipeline asset's renderer list: a renderer without the world's features (fog, water, " +
            "post effects). -1 = the default")] int _renderer = 1;
        [SerializeField, Min(64)] int _resolution = 1024;
        [SerializeField, Range(5f, 60f)] float _fieldOfView = 22f;
        [SerializeField, Tooltip("Degrees up and around the item at first: three-quarter on its right side (where lights and lasers " +
            "mount), muzzle to the right")] Vector2 _startAngles = new(14f, 120f);
        [SerializeField, Tooltip("Degrees per pixel dragged")] float _turnSpeed = 0.35f;
        [SerializeField] Vector2 _pitchLimits = new(-35f, 70f);
        [SerializeField] Vector2 _zoomLimits = new(0.55f, 1.6f);
        [SerializeField, Tooltip("Zoom change per wheel notch")] float _zoomStep = 0.1f;
        [SerializeField] Color _keyLight = new(1f, 0.96f, 0.9f, 1f);
        [SerializeField] float _keyIntensity = 3f;
        [SerializeField] Color _fillLight = new(0.65f, 0.75f, 1f, 1f);
        [SerializeField] float _fillIntensity = 1.6f;
        [SerializeField, Range(0f, 0.2f), Tooltip("Share of its size the model grows for a moment when a part is fitted (half when one comes off)")]
        float _nudge = 0.05f;
        [SerializeField, Min(0.05f)] float _nudgeSeconds = 0.28f;

        Transform _root;
        Transform _pivot;
        Camera _camera;
        RenderTexture _texture;
        GameObject _model;
        Bounds _bounds;
        Vector2 _angles;
        float _zoom = 1f;
        int _layerIndex;

        public Camera Camera => _camera;
        public GameObject Model => _model;
        public RectTransform Image => _image != null ? _image.rectTransform : null;

        // Shows `item`'s world model (null clears), framed and turned to the start angle.
        public void Show(ItemInstance item)
        {
            Clear();
            WorldItem prefab = item?.Definition.Prefab;
            if (prefab == null)
            {
                SetRendering(false);
                return;
            }
            EnsureRig();
            _model = Build(prefab);
            if (_model == null)
            {
                SetRendering(false);
                return;
            }
            Refresh(item);
            _bounds = Bounds(_model);
            // Unturned first, so the new model doesn't keep the inverse of the last one's turn.
            _pivot.SetPositionAndRotation(_bounds.center, Quaternion.identity);
            _model.transform.SetParent(_pivot, true);
            _angles = _startAngles;
            _zoom = 1f;
            SetRendering(true);
            Place();
        }

        // The item's own state changed (a part fitted): its views update, the framing stays.
        public void Refresh(ItemInstance item)
        {
            if (_model == null)
                return;
            foreach (IItemView view in _model.GetComponentsInChildren<IItemView>(true))
                view.ShowItem(item);
            _model.transform.SetLayerRecursively(_layerIndex);
            foreach (Light light in _model.GetComponentsInChildren<Light>(true))
                light.enabled = false;
        }

        // A part snapped on (or came off): a short punch of the model's size, on unscaled time (the bag may pause the game).
        public void Nudge(bool attached)
        {
            if (_model == null)
                return;
            Transform model = _model.transform;
            model.DOKill(true);
            model.DOPunchScale(Vector3.one * (attached ? _nudge : _nudge * 0.5f), _nudgeSeconds, 6, 0.6f).SetUpdate(true);
        }

        public void Turn(Vector2 pixels)
        {
            if (_model == null)
                return;
            _angles.y += pixels.x * _turnSpeed;
            _angles.x = Mathf.Clamp(_angles.x - pixels.y * _turnSpeed, _pitchLimits.x, _pitchLimits.y);
            Place();
        }

        public void Zoom(float notches)
        {
            if (_model == null)
                return;
            _zoom = Mathf.Clamp(_zoom - notches * _zoomStep, _zoomLimits.x, _zoomLimits.y);
            Place();
        }

        // Where a world point on the model shows in the RawImage's rect (local coordinates); false when behind the camera.
        public bool TryProject(Vector3 world, out Vector2 local)
        {
            local = default;
            if (_camera == null || _image == null || _model == null)
                return false;
            Vector3 viewport = _camera.WorldToViewportPoint(world);
            if (viewport.z <= 0f)
                return false;
            Rect rect = _image.rectTransform.rect;
            local = new Vector2(rect.xMin + viewport.x * rect.width, rect.yMin + viewport.y * rect.height);
            return true;
        }

        public void Hide()
        {
            Clear();
            SetRendering(false);
        }

        void OnDestroy()
        {
            if (_root != null)
                Destroy(_root.gameObject);
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        void Clear()
        {
            if (_model == null)
                return;
            _model.transform.DOKill();
            _model.SetActive(false);
            Destroy(_model);
            _model = null;
        }

        void SetRendering(bool on)
        {
            if (_camera != null)
                _camera.enabled = on;
            if (_image != null)
                _image.enabled = on;
        }

        // Its own root (not under the canvas), a camera rendering only the stage layer into a texture, a key and a fill light.
        void EnsureRig()
        {
            if (_root != null)
                return;
            _layerIndex = LayerMask.NameToLayer(_layer);
            if (_layerIndex < 0)
            {
                Debug.LogWarning($"{name}: no layer '{_layer}'; the item view uses Default.", this);
                _layerIndex = 0;
            }
            _root = new GameObject("ItemPreviewStage").transform;
            _root.position = _position;
            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(_root, false);

            Rect rect = _image != null ? _image.rectTransform.rect : new Rect(0f, 0f, 16f, 9f);
            float aspect = rect.height > 0f ? rect.width / rect.height : 16f / 9f;
            _texture = new RenderTexture(_resolution, Mathf.Max(64, Mathf.RoundToInt(_resolution / aspect)), 24, RenderTextureFormat.ARGB32)
                { name = "ItemPreview", antiAliasing = 4 };
            if (_image != null)
                _image.texture = _texture;

            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_root, false);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.cullingMask = 1 << _layerIndex;
            _camera.fieldOfView = _fieldOfView;
            _camera.nearClipPlane = 0.02f;
            _camera.farClipPlane = 20f;
            _camera.targetTexture = _texture;
            _camera.enabled = false;
            UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            if (_renderer >= 0)
                data.SetRenderer(_renderer);

            AddLight("Key", _keyLight, _keyIntensity, new Vector3(-1.2f, 1.4f, -1.4f));
            AddLight("Fill", _fillLight, _fillIntensity, new Vector3(1.6f, 0.2f, 1.2f));
        }

        void AddLight(string lightName, Color color, float intensity, Vector3 offset)
        {
            var light = new GameObject(lightName).AddComponent<Light>();
            light.transform.SetParent(_root, false);
            light.transform.localPosition = offset;
            light.type = LightType.Point;
            light.range = 6f;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << _layerIndex;
        }

        // A copy of the item prefab's world model only: the held parts, scripts that act and colliders are left behind.
        GameObject Build(WorldItem prefab)
        {
            var staging = new GameObject("Staging");
            staging.SetActive(false);
            staging.transform.SetParent(_root, false);
            WorldItem copy = Instantiate(prefab, staging.transform, false);
            GameObject model = copy.WorldModel;
            if (model == null)
            {
                Destroy(staging);
                return null;
            }
            model.transform.SetParent(_root, false);
            model.transform.localPosition = Vector3.zero;
            model.SetActive(true);
            // A model that shows the item's own state (a gun's parts) stands upright here, not lying as on the ground.
            if (model.GetComponentInChildren<IItemView>(true) is Component view)
                view.transform.localRotation = Quaternion.identity;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                Destroy(collider);
            Destroy(staging);
            return model;
        }

        static Bounds Bounds(GameObject model)
        {
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (renderer is not (MeshRenderer or SkinnedMeshRenderer))
                    continue;
                if (!any)
                    bounds = renderer.bounds;
                else
                    bounds.Encapsulate(renderer.bounds);
                any = true;
            }
            return bounds;
        }

        // Turns the pivot (not the camera) and backs the camera off far enough to frame the item's bounds.
        void Place()
        {
            _pivot.rotation = Quaternion.Euler(0f, _angles.y, 0f) * Quaternion.Euler(_angles.x, 0f, 0f);
            float radius = Mathf.Max(0.05f, _bounds.extents.magnitude);
            float distance = radius / Mathf.Sin(_fieldOfView * 0.5f * Mathf.Deg2Rad) * _zoom * 0.72f;
            _camera.transform.SetPositionAndRotation(_pivot.position - Vector3.forward * distance, Quaternion.identity);
        }
    }
}
