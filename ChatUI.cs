using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using DuloGames.UI;
using FirstGearGames.LobbyAndWorld.Demos.KingOfTheHill;
using Unity.Services.Vivox;
using Unity.Services.Vivox.AudioTaps;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections;
using _ZombieRoyale.Scripts.Core;
using Invector.vItemManager;
using FishNet;

namespace Vivox.DnD
{
    public class ChatUI : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [System.Serializable]
        public enum TextChannel : int
        {
            Global = 0,
            Team = 1,
            InGame = 2
        }

        public enum ChatState
        {
            Menu, Game
        }

        public enum TextCommand
        {
            all, team
        }

        [System.Serializable]
        public class SendMessageEvent : UnityEvent<int, string>
        {
        }

        [System.Serializable]
        public class TabInfo
        {
            public int id = 0;
            public UITab button;
            public Transform content;
            public ScrollRect scrollRect;
            public TextChannel textChannel;
        }

        public string ClassName => _className ??= $"{StringToHexColor.GetColoredClassName(GetType())}";
        private string _className;

        [SerializeField] private InputField m_InputField;
        [Header("Buttons")]
        [SerializeField] private Button m_SubmitButton;
        [SerializeField] private Button m_ScrollTopButton;
        [SerializeField] private Button m_ScrollBottomButton;
        [SerializeField] private Button m_ScrollUpButton;
        [SerializeField] private Button m_ScrollDownButton;

        [Header("Tab Properties")][SerializeField]
        private List<TabInfo> m_Tabs = new List<TabInfo>();

        public string GetUID = "UIDdefault";

        private Image _ChatFieldImage;
        private UIWindow _UIWindow;
        private UIInputEvent _UIInputEvent;
        private TabInfo m_ActiveTabInfo;

        private bool _isActiveChat;
        private bool _isActiveInputField;
        private ChatState _currentState = ChatState.Menu;

        public static ChatUI Instance;
        public static bool IsActiveted;

       /// <summary>
       /// Vivox components
       /// </summary>

       protected IList<KeyValuePair<string, ChatMessageObjectUI>> m_MessageObjPool = new List<KeyValuePair<string, ChatMessageObjectUI>>();
        public GameObject MessageObject;

        protected Task FetchMessages = null;
        protected DateTime? oldestMessage = null;

        /// <summary>
        /// Vivox components
        /// </summary>
        protected void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            // Find the active tab info
            this.m_ActiveTabInfo = this.FindActiveTab();

            // Clear the lines of text
            if (this.m_Tabs != null && this.m_Tabs.Count > 0)
            {
                foreach (TabInfo info in this.m_Tabs)
                {
                    // if we have a button
                    if (info.content != null)
                    {
                        foreach (Transform t in info.content)
                        {
                            Destroy(t.gameObject);
                        }
                    }
                }
            }
        }

        void Start()
        {
            _UIInputEvent = GetComponent<UIInputEvent>();
            _UIWindow = GetComponent<UIWindow>();
            _ChatFieldImage = GetComponent<Image>();

            VivoxService.Instance.ChannelJoined += OnChannelJoined;
            VivoxService.Instance.DirectedMessageReceived += OnDirectedMessageReceived;
            VivoxService.Instance.ChannelMessageReceived += OnChannelMessageReceived;

            if (this.m_Tabs != null && this.m_Tabs.Count > 0)
            {
                foreach (TabInfo info in this.m_Tabs)
                {
                    // if we have a button
                    if (info.scrollRect != null)
                    {
                        info.scrollRect.onValueChanged.AddListener(ScrollRectChange);
                    }
                }
            }           
        }

        protected void OnEnable()
        {
            // Hook the scroll up button click event
            if (this.m_ScrollUpButton != null)
            {
                this.m_ScrollUpButton.onClick.AddListener(OnScrollUpClick);
            }

            // Hook the scroll down button click event
            if (this.m_ScrollDownButton != null)
            {
                this.m_ScrollDownButton.onClick.AddListener(OnScrollDownClick);
            }

            // Hook the input field end edit event
            if (this.m_InputField != null)
            {
                this.m_InputField.onEndEdit.AddListener(OnInputEndEdit);
            }

            if (this.m_SubmitButton != null)
            {
                this.m_SubmitButton.onClick.AddListener(SendChatMessage);
            }

            // Hook the scroll to top button click event
            if (this.m_ScrollTopButton != null)
            {
                this.m_ScrollTopButton.onClick.AddListener(OnScrollToTopClick);
            }

            // Hook the scroll to bottom button click event
            if (this.m_ScrollBottomButton != null)
            {
                this.m_ScrollBottomButton.onClick.AddListener(OnScrollToBottomClick);
            }

            // Hook the tab toggle change events
            if (this.m_Tabs != null && this.m_Tabs.Count > 0)
            {
                foreach (TabInfo info in this.m_Tabs)
                {
                    // if we have a button
                    if (info.button != null)
                    {
                        info.button.onValueChanged.AddListener(OnTabStateChange);
                        info.button.onValueChanged.AddListener(OnChangeTabPanel);

                    }
                }            
            }

            //ClearTextField();
        }

        protected void OnDisable()
        {
            // Unhook the scroll up button click event
            if (this.m_ScrollUpButton != null)
            {
                this.m_ScrollUpButton.onClick.RemoveListener(OnScrollUpClick);
            }

            // Unhook the scroll down button click event
            if (this.m_ScrollDownButton != null)
            {
                this.m_ScrollDownButton.onClick.RemoveListener(OnScrollDownClick);
            }

            if (this.m_SubmitButton != null)
            {
                this.m_SubmitButton.onClick.RemoveListener(SendChatMessage);
            }

            // Unhook the scroll to top button click event
            if (this.m_ScrollTopButton != null)
            {
                this.m_ScrollTopButton.onClick.RemoveListener(OnScrollToTopClick);
            }

            // Unhook the scroll to bottom button click event
            if (this.m_ScrollBottomButton != null)
            {
                this.m_ScrollBottomButton.onClick.RemoveListener(OnScrollToBottomClick);
            }

            // Unhook the tab toggle change events
            if (this.m_Tabs != null && this.m_Tabs.Count > 0)
            {
                foreach (TabInfo info in this.m_Tabs)
                {
                    // if we have a button
                    if (info.button != null)
                    {
                        info.button.onValueChanged.RemoveListener(OnTabStateChange);
                    }
                }
            }

            if (m_MessageObjPool.Count > 0)
            {
                ClearMessageObjectPool();
            }

            oldestMessage = null;
        }

        protected void OnDestroy()
        {
            VivoxService.Instance.ChannelJoined -= OnChannelJoined;
            VivoxService.Instance.DirectedMessageReceived -= OnDirectedMessageReceived;
            VivoxService.Instance.ChannelMessageReceived -= OnChannelMessageReceived;

#if UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
            m_InputField.onEndEdit.RemoveAllListeners();
#endif
            if (this.m_Tabs != null && this.m_Tabs.Count > 0)
            {
                foreach (TabInfo info in this.m_Tabs)
                {
                    // if we have a button
                    if (info.scrollRect != null)
                    {
                        info.scrollRect.onValueChanged.RemoveAllListeners();
                    }
                }
            }
        }

        public void GenerateChannelID(string channelId)
        {
            if (!IsActiveted)
                return;

            if (!string.IsNullOrEmpty(channelId))
                GetUID = "UID" + channelId;
            else GetUID = "UIDdefault";
        }

        public void UpdateChatChannels(string state)
        {
            if (!IsActiveted)
                return;

            switch (state)
            {
                case nameof(ChatState.Menu):
                    _currentState = ChatState.Menu;
                    break;
                case nameof(ChatState.Game):
                    _currentState = ChatState.Game;
                    break;
                default:
                    break;
            }

            m_Tabs.ForEach(t => { 
                t.button.gameObject.SetActive(true); 
                t.button.isOn = false; 
                t.button.onValueChanged.Invoke(false); 
            });

            if (_currentState == ChatState.Menu) MainMenuUpdateChatChannels();
            else
            {
                StartCoroutine(InGameChatChannels());            
            }         
        }

        private void MainMenuUpdateChatChannels()
        {
            try
            {

          
            foreach (TabInfo info in this.m_Tabs)
            {
                if (info.button != null)
                {
                    if (info.textChannel == TextChannel.Team || info.textChannel == TextChannel.InGame)
                        info.button.gameObject.SetActive(false);
                }
            }
            _UIInputEvent.enabled = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{ClassName}[MainMenuUpdateChatChannels] {e.Message} {e.InnerException.Message}");
            }
        }

        private IEnumerator InGameChatChannels()
        {
            yield return new WaitUntil(() => GameplayManager.Instance != null);

            yield return new WaitUntil(() => GameplayManager.Instance.RoomDetails != null);

            m_Tabs.ForEach(x => {
                if (x.id == 3)
                {
                    x.button.isOn = true;
                    x.button.onValueChanged.Invoke(true);
                }
            });

            foreach (TabInfo info in this.m_Tabs)
            {
                // if we have a button
                if (info.button != null)
                {
                    if ((!GameplayManager.Instance.RoomDetails.IsTeamsMode && info.textChannel == TextChannel.Team) || info.textChannel == TextChannel.Global)
                    {
                        info.button.gameObject.SetActive(false);                     
                    }
                }
            }

            Debug.Log("StartConn: " + GetUID);

            VivoxService.Instance.JoinGroupChannelAsync(ChatUI.TextChannel.Team.ToString() + GetUID, ChatCapability.TextAndAudio);
            VivoxService.Instance.JoinGroupChannelAsync(ChatUI.TextChannel.InGame.ToString() + GetUID, ChatCapability.TextAndAudio);

            _UIInputEvent.enabled = true;
            OnChatPanelDeselect();
        }

        public bool IsChatOpened()
        {
            return _isActiveChat;
        }

        /// <summary>
        /// Fired when the scroll up button is pressed.
        /// </summary>
        public void OnScrollUpClick()
        {
            if (this.m_ActiveTabInfo == null || this.m_ActiveTabInfo.scrollRect == null)
                return;

            PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
            pointerEventData.scrollDelta = new Vector2(0f, 1f);

            this.m_ActiveTabInfo.scrollRect.OnScroll(pointerEventData);
        }

        /// <summary>
        /// Fired when the scroll down button is pressed.
        /// </summary>
        public void OnScrollDownClick()
        {
            if (this.m_ActiveTabInfo == null || this.m_ActiveTabInfo.scrollRect == null)
                return;

            PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
            pointerEventData.scrollDelta = new Vector2(0f, -1f);

            this.m_ActiveTabInfo.scrollRect.OnScroll(pointerEventData);
        }

        /// <summary>
        /// Fired when the scroll to top button is pressed.
        /// </summary>
        public void OnScrollToTopClick()
        {
            if (this.m_ActiveTabInfo == null || this.m_ActiveTabInfo.scrollRect == null)
                return;

            // Scroll to top
            this.m_ActiveTabInfo.scrollRect.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// Fired when the scroll to bottom button is pressed.
        /// </summary>
        public void OnScrollToBottomClick()
        {
            if (this.m_ActiveTabInfo == null || this.m_ActiveTabInfo.scrollRect == null)
                return;

            // Scroll to bottom
            this.m_ActiveTabInfo.scrollRect.verticalNormalizedPosition = 0f;
        }

        private void OnChangeTabPanel(bool value)
        {
            OnChatPanelSelect();

            if (EscapeMenu.Instance)
            {
                m_InputField.Select();
            }
        }

        public void OnChatPanelDeselectEvent()
        {
            if (_currentState == ChatState.Menu) OnChatPanelDeselect();
        }

        public void OnChatPanelSelectEvent()
        {
            if (_currentState == ChatState.Menu) OnChatPanelSelect();
        }

        public void OnChatPanelDeselect()
        {
            if (!_isActiveInputField || (_isActiveInputField && _isActiveChat))
            {
                _isActiveChat = false;
                GetComponent<CanvasGroup>().alpha = 0.5f;

                ToggleInputs(true);
                GameStateController.Instance.SetState(GameStateController.GameState.Default);
            }
        }

        public void OnChatPanelSelect()
        {
            if (_isActiveInputField || (!_isActiveInputField && !_isActiveChat))
            {
                _isActiveChat = true;
                GetComponent<CanvasGroup>().alpha = 1f;

                ToggleInputs(false);
                GameStateController.Instance.SetState(GameStateController.GameState.ChatOpen);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {          
            ((ISelectHandler)m_InputField).OnSelect(eventData);
            OnInputFieldSelect();
        }

        public void OnDeselect(BaseEventData eventData)
        {          
            ((IDeselectHandler)m_InputField).OnDeselect(eventData);
            OnInputFieldDeselect();
        }

        public void OnInputFieldSelect()
        {
            _isActiveInputField = true;

            if (_currentState == ChatState.Game)
            {
                EscapeMenu.Instance.UIWindow.enabled = false;
                ToggleInputs(false);
                GameStateController.Instance.SetState(GameStateController.GameState.ChatOpen);
            }

            if (!_isActiveChat)
                OnChatPanelSelect();
        }

        public void OnInputFieldDeselect()
        {
            _isActiveInputField = false;

            if (_currentState == ChatState.Game)
            {
                EscapeMenu.Instance.UIWindow.enabled = true;
                ToggleInputs(true);
                GameStateController.Instance.SetState(GameStateController.GameState.Default);
            }

            if (_isActiveChat)
                OnChatPanelDeselect();
        }

        /// <summary>
        /// Fired when the input field is submitted.
        /// </summary>
        /// <param name="text"></param>
        private void OnInputEndEdit(string text)
        {
            // Make sure the return key is pressed
            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (string.IsNullOrEmpty(text))
                {
                    StartCoroutine(SendMessageDelay());
                    return;
                }
                // Send the message
                this.SendChatMessage();
            }
        }

        private void ToggleInputs(bool enable)
        {

            if (GameplayManager.Instance == null || UnitComponentsManager.GetAllPlayersHeroes().Count == 0)
                return;

            var nob = UnitComponentsManager.GetAllPlayersHeroes().First();
            var unit = UnitComponentsManager.GetUnitComponents(nob);

            if (unit.InvectorManager.Inventory.isOpen)
                return;

            unit.InvectorManager.Inventory.lockInventoryInput = !enable;

            RoomSystemHandler.ShowCursor(!enable);
            RoomSystemHandler.LockCursor(enable);
        }

        public void ChatActivate()
        {
            if (!_isActiveInputField)
            {
                m_InputField.Select();
                GetComponent<CanvasGroup>().alpha = 1f;
            }
        }

        void ClearTextField()
        {
            if (m_InputField != null)
            {
                m_InputField.text = string.Empty;
                m_InputField.Select();
                m_InputField.ActivateInputField();
            }
        }

        /// <summary>
        /// Fired when a tab button is toggled.
        /// </summary>
        /// <param name="state"></param>
        public void OnTabStateChange(bool state)
        {       
            // If a tab was activated
            if (state)
            {
                // Find the active tab
                this.m_ActiveTabInfo = this.FindActiveTab();
            }
        }

        /// <summary>
        /// Finds the active tab based on the tab buttons toggle state.
        /// </summary>
        /// <returns>The active tab info.</returns>
        private TabInfo FindActiveTab()
        {

            // If we have tabs
            if (this.m_Tabs != null && this.m_Tabs.Count > 0)
            {
                foreach (TabInfo info in this.m_Tabs)
                {
                    // if we have a button
                    if (info.button != null)
                    {
                        // If this button is active
                        if (info.button.isOn)
                        {
                            return info;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the tab info for the specified tab by id.
        /// </summary>
        /// <param name="tabId">Tab id.</param>
        /// <returns></returns>
        public TabInfo GetTabInfo(int tabId)
        {
            // If we have tabs
            if (this.m_Tabs != null && this.m_Tabs.Count > 0)
            {
                foreach (TabInfo info in this.m_Tabs)
                {
                    // If this is the tab we are looking for
                    if (info.id == tabId)
                    {
                        return info;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Sends a chat message.
        /// </summary>
        /// <param name="text">The message.</param>
        private void SendChatMessage()
        {
            if (string.IsNullOrEmpty(m_InputField.text))
            {
                return;
            }

            int tabId = (this.m_ActiveTabInfo != null ? this.m_ActiveTabInfo.id : 0);
            TabInfo tabInfo = this.GetTabInfo(tabId);

            // Make sure we have tab info
            if (tabInfo == null || tabInfo.content == null)
                return;

            Debug.Log("SendMsg: " + m_ActiveTabInfo.textChannel.ToString() + GetUID);
            VivoxService.Instance.SendChannelTextMessageAsync(m_ActiveTabInfo.textChannel.ToString() + GetUID, m_InputField.text);

           // ClearTextField();
            m_InputField.text = string.Empty;

            //    if(_currentState == ChatState.Game)
            //         GetComponent<UIWindow>().ApplyVisualState(UIWindow.VisualState.Hidden);

            StartCoroutine(SendMessageDelay());
        }

        IEnumerator SendMessageDelay()
        {
            yield return new WaitForEndOfFrame();

            OnInputFieldDeselect();

            if (EventSystem.current.currentSelectedGameObject == m_InputField.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        protected void ScrollRectChange(Vector2 vector)
        {
            // Scrolled near end and check if we are fetching history already
            if (m_ActiveTabInfo.scrollRect.verticalNormalizedPosition >= 0.95f && FetchMessages != null && (FetchMessages.IsCompleted || FetchMessages.IsFaulted || FetchMessages.IsCanceled))
            {
                m_ActiveTabInfo.scrollRect.normalizedPosition = new Vector2(0, 0.8f);
              //  FetchMessages = FetchHistory(false);
            }
        }

        protected async Task FetchHistory(bool scrollToBottom = false)
        {
            try
            {
                var chatHistoryOptions = new ChatHistoryQueryOptions()
                {
                    TimeEnd = oldestMessage
                };
                var historyMessages =
                    await VivoxService.Instance.GetChannelTextMessageHistoryAsync(m_ActiveTabInfo.textChannel.ToString() + GetUID, 10,
                        chatHistoryOptions);
                var reversedMessages = historyMessages.Reverse();
                foreach (var historyMessage in reversedMessages)
                {
                    AddMessageToChat(historyMessage, true, scrollToBottom);
                }

                // Update the oldest message ReceivedTime if it exists to help the next fetch get the next batch of history
                oldestMessage = historyMessages.FirstOrDefault()?.ReceivedTime;
            }
            catch (TaskCanceledException e)
            {
                Debug.Log($"Chat history request was canceled, likely because of a logout or the data is no longer needed: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Tried to fetch chat history and failed with error: {e.Message}");
            }
        }     

        protected void ChannelEffectValueChanged(int value)
        {
            AudioTapsManager.Instance.AddChannelAudioEffect((AudioTapsManager.Effects)value);
        }

        protected void ClearMessageObjectPool()
        {
            foreach (KeyValuePair<string, ChatMessageObjectUI> keyValuePair in m_MessageObjPool)
            {
                Destroy(keyValuePair.Value.gameObject);
            }
            m_MessageObjPool.Clear();
        }

        IEnumerator SendScrollRectToBottom()
        {
            yield return new WaitForEndOfFrame();

            // We need to wait for the end of the frame for this to be updated, otherwise it happens too quickly.
            m_ActiveTabInfo.scrollRect.normalizedPosition = new Vector2(0, 0);

            yield return null;
        }

        protected void OnDirectedMessageReceived(VivoxMessage message)
        {
            AddMessageToChat(message, false, true);
        }

        protected async void OnChannelJoined(string channelName)
        {
           await VivoxService.Instance.SetChannelTransmissionModeAsync(TransmissionMode.None, channelName);
        }

        protected void OnChannelMessageReceived(VivoxMessage message)
        {
          
            AddMessageToChat(message, false, true);
        }

        protected void AddMessageToChat(VivoxMessage message, bool isHistory = false, bool scrollToBottom = false)
        {

            int tabId = (this.m_ActiveTabInfo != null ? this.m_ActiveTabInfo.id : 0);
            TabInfo tabInfo = this.GetTabInfo(tabId);

            var newMessageObj = Instantiate(MessageObject, m_ActiveTabInfo.content.transform);
            var newMessageTextObject = newMessageObj.GetComponent<ChatMessageObjectUI>();
            if (isHistory)
            {
                m_MessageObjPool.Insert(0, new KeyValuePair<string, ChatMessageObjectUI>(message.MessageId, newMessageTextObject));
                newMessageObj.transform.SetSiblingIndex(0);
            }
            else
            {
                m_MessageObjPool.Add(new KeyValuePair<string, ChatMessageObjectUI>(message.MessageId, newMessageTextObject));
            }

            newMessageTextObject.SetTextMessage(message, tabInfo,false);

            if (scrollToBottom)
            {
                StartCoroutine(SendScrollRectToBottom());
            }         
        }
    }
}