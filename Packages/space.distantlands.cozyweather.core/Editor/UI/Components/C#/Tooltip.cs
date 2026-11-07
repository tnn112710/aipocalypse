//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [UxmlElement]
    public partial class Tooltip : VisualElement
    {
        [UxmlAttribute]
        public string Message
        {
            get => message; set
            {
                message = value;
                if (MessageBox != null)
                    MessageBox.text = Message;
                MarkDirtyRepaint();
            }
        }
        private string message;
        [UxmlAttribute]
        public StatusType messageType
        {
            get => _messageType; set
            {
                _messageType = value;
                MarkDirtyRepaint();
            }
        }
        private StatusType _messageType;
        private Label MessageBox => this.Q<Label>();



        public Tooltip()
        {
            Init();
        }

        public Tooltip(string message)
        {
            Message = message;
            Init();
        }
        public Tooltip(string message, StatusType tooltipType)
        {
            Message = message;
            _messageType = tooltipType;
            Init();
        }

        public void Init()
        {
            RegisterCallback<AttachToPanelEvent>(e =>
                {
                    Clear();

                    if (!CozyPreferences.Tooltips) return;

                    style.display = CozyPreferences.Tooltips ? DisplayStyle.Flex : DisplayStyle.None;

                    VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                        "Packages/space.distantlands.cozyweather.core/Editor/UI/Components/UXML/tooltip.uxml"
                    );
                    asset.CloneTree(this);
                    Refresh();
                });
        }

        public void Refresh()
        {
            RemoveFromClassList("info");
            RemoveFromClassList("pass");
            RemoveFromClassList("warning");
            RemoveFromClassList("fail");

            MessageBox.text = Message;

            this.Q<VisualElement>(className: "tooltip").AddToClassList(messageType.ToString().ToLower());

        }
    }
}