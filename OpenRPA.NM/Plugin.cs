﻿using OpenRPA.Interfaces;
using OpenRPA.Interfaces.Selector;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenRPA.NM
{
    public class Plugin : ObservableObject, IRecordPlugin
    {
        public NMElement LastElement { get; set; }
        public string Name => "NM";
        public string Status => (NMHook.connected ? "online" : "offline");
        public event Action<IRecordPlugin, IRecordEvent> OnUserAction;
        public event Action<IRecordPlugin, IRecordEvent> OnMouseMove
        {
            add { }
            remove { }
        }
        public System.Windows.Controls.UserControl editor => null;
        public IElement[] GetElementsWithSelector(Selector selector, IElement fromElement = null, int maxresults = 1)
        {
            if (!(selector is NMSelector nmselector))
            {
                nmselector = new NMSelector(selector.ToString());
            }
            var result = NMSelector.GetElementsWithuiSelector(nmselector, fromElement, maxresults);
            return result;
        }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "IDE1006")]
        public static treeelement[] _GetRootElements(Selector anchor)
        {
            var rootelements = new List<treeelement>();
            NMHook.reloadtabs();
            // var tab = NMHook.tabs.Where(x => x.highlighted == true && x.browser == "chrome").FirstOrDefault();
            var tab = NMHook.tabs.Where(x => x.highlighted == true).FirstOrDefault();
            if (tab == null)
            {
                // tab = NMHook.tabs.Where(x => x.browser == "chrome").FirstOrDefault();
                tab = NMHook.tabs.FirstOrDefault();
            }
            if (NMHook.tabs.Count == 0) { return rootelements.ToArray(); }
            // getelement.data = "getdom";
            var getelement = new NativeMessagingMessage("getelement")
            {
                browser = tab.browser,
                tabid = tab.id,

                xPath = "/html"
            };
            if (anchor != null && anchor.Count > 1)
            {
                var s = anchor[1];
                var p = s.Properties.Where(x => x.Name == "xpath").FirstOrDefault();
                if (p != null) getelement.xPath = p.Value;
            }

            NativeMessagingMessage result = null;
            try
            {
                result = NMHook.sendMessageResult(getelement, true, TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
            }
            if (result != null && result.result != null && result.results == null)
            {
                result.results = new NativeMessagingMessage[] { result };
            }
            if (result != null && result.results != null && result.results.Count() > 0)
            {
                foreach (var res in result.results)
                {
                    if (res.result != null)
                    {
                        //var html = new HtmlElement(getelement.xPath, getelement.cssPath, res.tabid, res.frameId, res.result);
                        var html = new NMElement(res);
                        rootelements.Add(new NMTreeElement(null, true, html));
                    }
                }
                //result = result.results[0];
            }
            return rootelements.ToArray();
        }
        public treeelement[] GetRootElements(Selector anchor)
        {
            return Plugin._GetRootElements(anchor);
        }
        public Selector GetSelector(Selector anchor, treeelement item)
        {
            var nmitem = item as NMTreeElement;
            NMSelector nmanchor = anchor as NMSelector;
            if (nmitem == null && anchor != null)
            {
                nmanchor = new NMSelector(anchor.ToString());
            }
            if (nmanchor != null)
            {
                var element = GetElementsWithSelector(nmanchor);
                return new NMSelector(nmitem.NMElement, nmanchor, true, (NMElement)element.FirstOrDefault());

            }
            return new NMSelector(nmitem.NMElement, nmanchor, true, null);
        }
        public void Initialize(IOpenRPAClient client)
        {
            NMHook.registreChromeNativeMessagingHost(false);
            NMHook.registreffNativeMessagingHost(false);
            NMHook.checkForPipes(true, true);
            NMHook.onMessage += OnMessage;
            NMHook.Connected += OnConnected;
            NMHook.onDisconnected += OnDisconnected;
        }
        private void OnConnected(string obj)
        {
            NotifyPropertyChanged("Status");
        }
        private void OnDisconnected(string obj)
        {
            NotifyPropertyChanged("Status");
        }
        private void OnMessage(NativeMessagingMessage message)
        {
            try
            {
                if (message.uiy > 0 && message.uix > 0 && message.uiwidth > 0 && message.uiheight > 0)
                {
                    if (!string.IsNullOrEmpty(message.data))
                    {
                        LastElement = new NMElement(message);
                    }
                    else
                    {
                        LastElement = new NMElement(message);
                    }
                }

                if (message.functionName == "click")
                {
                    if (IsRecording)
                    {
                        if (LastElement == null) return;
                        var re = new RecordEvent
                        {
                            Button = Input.MouseButton.Left
                        }; var a = new GetElement { DisplayName = LastElement.ToString() };

                        var selector = new NMSelector(LastElement, null, true, null);
                        a.Selector = selector.ToString();
                        a.Image = LastElement.ImageString();
                        a.MaxResults = 1;

                        re.Selector = selector;
                        re.a = new GetElementResult(a);
                        re.SupportInput = LastElement.SupportInput;
                        re.SupportSelect = false;
                        re.ClickHandled = true;
                        OnUserAction?.Invoke(this, re);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }
        public IElement LaunchBySelector(Selector selector, bool CheckRunning, TimeSpan timeout)
        {
            var first = selector[0];
            var second = selector[1];
            var p = first.Properties.Where(x => x.Name == "browser").FirstOrDefault();
            string browser = "";
            if (p != null) { browser = p.Value; }

            p = first.Properties.Where(x => x.Name == "url").FirstOrDefault();
            string url = "";
            if (p != null) { url = p.Value; }

            NMHook.openurl(browser, url);
            return null;
        }
        public void CloseBySelector(Selector selector, TimeSpan timeout, bool Force)
        {
            var first = selector[0];
            var second = selector[1];
            var p = first.Properties.Where(x => x.Name == "browser").FirstOrDefault();
            string browser = "";
            if (p != null) { browser = p.Value; }

            p = first.Properties.Where(x => x.Name == "url").FirstOrDefault();
            string url = "";
            if (p != null) { url = p.Value; }
            NMHook.reloadtabs();
            var tabs = NMHook.tabs.Where(x => x.browser == browser && x.url == url).ToList();
            if (string.IsNullOrEmpty(url)) tabs = NMHook.tabs.Where(x => x.browser == browser).ToList();
            foreach (var tab in tabs)
            {
                NMHook.CloseTab(tab);
            }
        }
        public bool Match(SelectorItem item, IElement m)
        {
            var el = new NMElement(m.RawElement as NativeMessagingMessage);
            return NMSelectorItem.Match(item, el);
        }
        public bool ParseUserAction(ref IRecordEvent e)
        {
            if (e.UIElement == null) return false;

            if (e.UIElement.ProcessId < 1) return false;
            var p = System.Diagnostics.Process.GetProcessById(e.UIElement.ProcessId);
            if (p.ProcessName.ToLower() != "chrome" && p.ProcessName.ToLower() != "firefox") return false;

            if (p.ProcessName.ToLower() == "chrome" && !NMHook.chromeconnected)
            {
                System.Windows.MessageBox.Show("You clicked inside Chrome, but it looks like you dont have the OpenRPA plugin installed");
                return false;
            }
            if (p.ProcessName.ToLower() == "firefox" && !NMHook.ffconnected)
            {
                System.Windows.MessageBox.Show("You clicked inside Firefix, but it looks like you dont have the OpenRPA plugin installed");
                return false;
            }
            if (LastElement == null) return false;
            var selector = new NMSelector(LastElement, null, true, null);
            var a = new GetElement { DisplayName = LastElement.id + " " + LastElement.type + " " + LastElement.Name };
            a.Selector = selector.ToString();
            a.Image = LastElement.ImageString();
            a.MaxResults = 1;

            e.Element = LastElement;
            e.Selector = selector;
            e.a = new GetElementResult(a);
            e.SupportInput = LastElement.SupportInput;
            e.SupportSelect = false;
            e.ClickHandled = true;
            e.OffsetX = e.X - LastElement.Rectangle.X;
            e.OffsetY = e.Y - LastElement.Rectangle.Y;
            LastElement.Click(true, e.Button, e.X, e.Y, false, false);
            return true;
        }
        public bool IsRecording { get; set; } = false;
        public void Start()
        {
            IsRecording = true;
        }
        public void Stop()
        {
            IsRecording = false;
        }
        public bool ParseMouseMoveAction(ref IRecordEvent e)
        {
            if (e.UIElement == null) return false;

            if (e.UIElement.ProcessId < 1) return false;
            var p = System.Diagnostics.Process.GetProcessById(e.UIElement.ProcessId);
            if (p.ProcessName.ToLower() != "chrome" && p.ProcessName.ToLower() != "firefox") return false;
            e.Element = LastElement;
            e.OffsetX = e.X - LastElement.Rectangle.X;
            e.OffsetY = e.Y - LastElement.Rectangle.Y;

            return true;
        }
    }
    public class GetElementResult : IBodyActivity
    {
        public GetElementResult(GetElement activity)
        {
            Activity = activity;
        }
        public Activity Activity { get; set; }
        public void AddActivity(Activity a, string Name)
        {
            var aa = new ActivityAction<NMElement>();
            var da = new DelegateInArgument<NMElement>
            {
                Name = Name
            };
            aa.Handler = a;
            ((GetElement)Activity).Body = aa;
            aa.Argument = da;
        }
        public void AddInput(string value, IElement element)
        {
            try
            {
                AddActivity(new System.Activities.Statements.Assign<string>
                {
                    To = new Microsoft.VisualBasic.Activities.VisualBasicReference<string>("item.value"),
                    Value = value
                }, "item");
                element.Value = value;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }
    }
    public class RecordEvent : IRecordEvent
    {
        public RecordEvent() { SupportVirtualClick = true; }
        public UIElement UIElement { get; set; }
        public IElement Element { get; set; }
        public Selector Selector { get; set; }
        public IBodyActivity a { get; set; }
        public bool SupportInput { get; set; }
        public bool SupportSelect { get; set; }
        public bool ClickHandled { get; set; }
        public bool SupportVirtualClick { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public Input.MouseButton Button { get; set; }
    }
}
