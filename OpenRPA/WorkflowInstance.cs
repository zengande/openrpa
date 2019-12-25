﻿using Newtonsoft.Json;
using OpenRPA.Interfaces;
using OpenRPA.Interfaces.entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenRPA
{
    public class WorkflowInstance : apibase, IWorkflowInstance
    {
        public WorkflowInstance()
        {
            _type = "workflowinstance";
            _id = Guid.NewGuid().ToString().Replace("{", "").Replace("}", "").Replace("-", "");
            // LastUpdated = DateTime.Now;
        }
        [JsonIgnore]
        // public DateTime LastUpdated { get { return GetProperty<DateTime>(); } set { SetProperty(value); } } 
        public static List<WorkflowInstance> Instances = new List<WorkflowInstance>();
        public event VisualTrackingHandler OnVisualTracking;
        public event idleOrComplete OnIdleOrComplete;
        public Dictionary<string, object> Parameters { get { return GetProperty<Dictionary<string, object>>(); } set { SetProperty(value); } }
        public Dictionary<string, object> Bookmarks { get { return GetProperty<Dictionary<string, object>>(); } set { SetProperty(value); } }
        [JsonIgnore]
        public string Path { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string correlationId { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string queuename { get { return GetProperty<string>(); } set { SetProperty(value); } }
        [JsonIgnore]
        public Dictionary<string, WorkflowInstanceValueType> Variables { get { return GetProperty<Dictionary<string, WorkflowInstanceValueType>>(); } set { SetProperty(value); } }
        public string InstanceId { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string WorkflowId { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string caller { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string RelativeFilename { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string xml { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string owner { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string ownerid { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string host { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string fqdn { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string errormessage { get { return GetProperty<string>(); } set { SetProperty(value); } }
        public string errorsource { get { return GetProperty<string>(); } set { SetProperty(value); } }
        [JsonIgnore]
        public Exception Exception { get { return GetProperty<Exception>(); } set { SetProperty(value); } }
        public bool isCompleted { get { return GetProperty<bool>(); } set { SetProperty(value); } }
        public bool hasError { get { return GetProperty<bool>(); } set { SetProperty(value); } }
        public string state { get { return GetProperty<string>(); } set { SetProperty(value); } }
        [JsonIgnore]
        public Workflow Workflow { get { return GetProperty<Workflow>(); } set { SetProperty(value); } }
        [JsonIgnore]
        public System.Activities.WorkflowApplication wfApp { get; set; }
        private void NotifyCompleted()
        {
            var _ref = (this as IWorkflowInstance);
            foreach (var runner in Plugins.runPlugins)
            {
                runner.onWorkflowCompleted(ref _ref);
            }
        }
        private void NotifyIdle()
        {
            var _ref = (this as IWorkflowInstance);
            foreach (var runner in Plugins.runPlugins)
            {
                runner.onWorkflowIdle(ref _ref);
            }
        }
        private void NotifyAborted()
        {
            var _ref = (this as IWorkflowInstance);
            foreach (var runner in Plugins.runPlugins)
            {
                runner.onWorkflowAborted(ref _ref);
            }
        }
        public static WorkflowInstance Create(Workflow Workflow, Dictionary<string, object> Parameters)
        {
            var result = new WorkflowInstance() { Workflow = Workflow, WorkflowId = Workflow._id, Parameters = Parameters, name = Workflow.name, Path = Workflow.Project.Path };
            result.RelativeFilename = Workflow.RelativeFilename;
            var _ref = (result as IWorkflowInstance);
            foreach (var runner in Plugins.runPlugins)
            {
                if (!runner.onWorkflowStarting(ref _ref, false)) throw new Exception("Runner plugin " + runner.Name + " declined running workflow instance");
            }
            if (global.isConnected)
            {
                result.owner = global.webSocketClient.user.name;
                result.ownerid = global.webSocketClient.user._id;
            }
            result.host = Environment.MachineName.ToLower();
            result.fqdn = System.Net.Dns.GetHostEntry(Environment.MachineName).HostName.ToLower();
            result.createApp();
            Instances.Add(result);
            foreach (var i in Instances.ToList())
            {
                if (i.isCompleted) Instances.Remove(i);
            }
            return result;
        }
        public void createApp()
        {
            //var xh = new XamlHelper(workflow.xaml);
            //extraextension.updateProfile(xh.Variables.ToArray(), xh.ArgumentNames.ToArray());
            var CustomTrackingParticipant = new WorkflowTrackingParticipant();
            CustomTrackingParticipant.OnVisualTracking += Participant_OnVisualTracking;

            if (string.IsNullOrEmpty(InstanceId))
            {
                // Remove unknown Parameters, if we don't the workflow will fail
                foreach (var param in Parameters.ToList())
                {
                    var allowed = Workflow.Parameters.Where(x => x.name == param.Key).FirstOrDefault();
                    if (allowed == null || allowed.direction == workflowparameterdirection.@out)
                    {
                        Parameters.Remove(param.Key);
                    }
                }
                // Ensure Type
                foreach (var wfparam in Workflow.Parameters)
                {
                    if (Parameters.ContainsKey(wfparam.name) && wfparam.type == "System.Int32")
                    {
                        if (Parameters[wfparam.name] != null)
                        {
                            Parameters[wfparam.name] = int.Parse(Parameters[wfparam.name].ToString());
                        }
                    }
                    else if (Parameters.ContainsKey(wfparam.name) && wfparam.type == "System.Boolean")
                    {
                        if (Parameters[wfparam.name] != null)
                        {
                            Parameters[wfparam.name] = bool.Parse(Parameters[wfparam.name].ToString());
                        }
                    }
                }
                wfApp = new System.Activities.WorkflowApplication(Workflow.Activity, Parameters);
                wfApp.Extensions.Add(CustomTrackingParticipant);
                if (Workflow.Serializable )
                {
                    //if (Config.local.localstate)
                    //{
                    //    if (!System.IO.Directory.Exists(System.IO.Directory.GetCurrentDirectory() + "\\state")) System.IO.Directory.CreateDirectory(System.IO.Directory.GetCurrentDirectory() + "\\state");
                    //    wfApp.InstanceStore = new Store.XMLFileInstanceStore(System.IO.Directory.GetCurrentDirectory() + "\\state");
                    //}
                    //else
                    //{
                    //    wfApp.InstanceStore = new Store.OpenFlowInstanceStore();
                    //}
                    wfApp.InstanceStore = new Store.OpenFlowInstanceStore();
                }
                addwfApphandlers(wfApp);
            }
            else
            {
                wfApp = new System.Activities.WorkflowApplication(Workflow.Activity);
                wfApp.Extensions.Add(CustomTrackingParticipant);
                addwfApphandlers(wfApp);
                if (Workflow.Serializable || !Workflow.Serializable)
                {
                    //if (Config.local.localstate)
                    //{
                    //    if (!System.IO.Directory.Exists(System.IO.Directory.GetCurrentDirectory() + "\\state")) System.IO.Directory.CreateDirectory(System.IO.Directory.GetCurrentDirectory() + "\\state");
                    //    wfApp.InstanceStore = new Store.XMLFileInstanceStore(System.IO.Directory.GetCurrentDirectory() + "\\state");
                    //}
                    //else
                    //{
                    //    wfApp.InstanceStore = new Store.OpenFlowInstanceStore();
                    //}
                    wfApp.InstanceStore = new Store.OpenFlowInstanceStore();
                }
                wfApp.Load(new Guid(InstanceId));
            }
            state = "loaded";
        }
        private void Participant_OnVisualTracking(WorkflowInstance Instance, string ActivityId, string ChildActivityId, string State)
        {
            OnVisualTracking?.Invoke(Instance, ActivityId, ChildActivityId, State);
        }
        public void Abort(string Reason)
        {
            if (wfApp == null) return;
            var _state = typeof(System.Activities.WorkflowApplication).GetField("state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(wfApp);
            if (_state.ToString() != "Aborted")
            {
                wfApp.Abort(Reason);
                return;
            }
            hasError = true;
            isCompleted = true;
            state = "aborted";
            Exception = new Exception(Reason);
            errormessage = Reason;
            Save();
            if (runWatch != null) runWatch.Stop();
            OnIdleOrComplete?.Invoke(this, EventArgs.Empty);
        }
        public void ResumeBookmark(string bookmarkName, object value)
        {
            try
            {
                Log.Verbose("[workflow] Resume workflow at bookmark '" + bookmarkName + "'");
                if (isCompleted)
                {
                    throw new ArgumentException("cannot resume bookmark on completed workflow!");
                }
                var _ref = (this as IWorkflowInstance);
                foreach (var runner in Plugins.runPlugins)
                {
                    if (!runner.onWorkflowResumeBookmark(ref _ref, bookmarkName, value)) throw new Exception("Runner plugin " + runner.Name + " declined running workflow instance");
                }
                // Log.Debug(String.Format("Workflow {0} resuming at bookmark '{1}' value '{2}'", wfApp.Id.ToString(), bookmarkName, value));
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(50);
                    try
                    {
                        wfApp.ResumeBookmark(bookmarkName, value);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }
                });
                state = "running";
                // Log.Debug(String.Format("Workflow {0} resumed bookmark '{1}' value '{2}'", wfApp.Id.ToString(), bookmarkName, value));
                Save();
            }
            catch (Exception)
            {
                throw;
            }
        }
        public System.Diagnostics.Stopwatch runWatch { get; set; }
        apibase IWorkflowInstance.Workflow { get => this.Workflow; set => this.Workflow = value as Workflow; }
        public void Run()
        {
            try
            {
                runWatch = new System.Diagnostics.Stopwatch();
                runWatch.Start();
                if (string.IsNullOrEmpty(InstanceId))
                {
                    wfApp.Run();
                    InstanceId = wfApp.Id.ToString();
                    state = "running";
                    Save();
                }
                else
                {
                    foreach (var b in Bookmarks)
                    {
                        if (b.Value != null && !string.IsNullOrEmpty(b.Value.ToString())) wfApp.ResumeBookmark(b.Key, b.Value);
                    }
                    if(Bookmarks.Count() == 0)
                    {
                        wfApp.Run();
                    }
                    state = "running";
                    Save();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "");
                hasError = true;
                isCompleted = true;
                //isUnloaded = true;
                state = "failed";
                Exception = ex;
                errormessage = ex.Message;
                Save();
                if (runWatch != null) runWatch.Stop();
                OnIdleOrComplete?.Invoke(this, EventArgs.Empty);
            }
        }
        private void addwfApphandlers(System.Activities.WorkflowApplication wfApp)
        {
            wfApp.Completed = delegate (System.Activities.WorkflowApplicationCompletedEventArgs e)
            {
                isCompleted = true;
                if (e.CompletionState == System.Activities.ActivityInstanceState.Faulted)
                {
                }
                else if (e.CompletionState == System.Activities.ActivityInstanceState.Canceled)
                {
                }
                else if (e.CompletionState == System.Activities.ActivityInstanceState.Closed)
                {
                    state = "completed";
                    foreach (var o in e.Outputs) Parameters[o.Key] = o.Value;
                    if (runWatch != null) runWatch.Stop();
                    NotifyCompleted();
                    OnIdleOrComplete?.Invoke(this, EventArgs.Empty);
                }
                else if (e.CompletionState == System.Activities.ActivityInstanceState.Executing)
                {
                }
                else
                {
                    throw new Exception("Unknown completetion state!!!" + e.CompletionState);
                }
            };

            wfApp.Aborted = delegate (System.Activities.WorkflowApplicationAbortedEventArgs e)
            {
                hasError = true;
                isCompleted = true;
                state = "aborted";
                Exception =  e.Reason;
                errormessage = e.Reason.Message;
                Save();
                if(runWatch!=null) runWatch.Stop();
                NotifyAborted();
                OnIdleOrComplete?.Invoke(this, EventArgs.Empty);
            };

            wfApp.Idle = delegate (System.Activities.WorkflowApplicationIdleEventArgs e)
            {
                var bookmarks = new Dictionary<string, object>();
                foreach (var b in e.Bookmarks)
                {
                    bookmarks.Add(b.BookmarkName, null);
                }
                Bookmarks = bookmarks;
                state = "idle";
                Save();
                if (state != "completed")
                {
                    NotifyIdle();
                    OnIdleOrComplete?.Invoke(this, EventArgs.Empty);
                }
            };

            wfApp.PersistableIdle = delegate (System.Activities.WorkflowApplicationIdleEventArgs e)
            {
                //return PersistableIdleAction.Unload;
                Save();
                return System.Activities.PersistableIdleAction.Persist;
            };

            wfApp.Unloaded = delegate (System.Activities.WorkflowApplicationEventArgs e)
            {
                if (!isCompleted && !hasError)
                {
                    state = "unloaded";

                } else
                {
                    DeleteFile();
                }
                //isUnloaded = true;
                if(global.isConnected)
                {
                    Save();
                }
            };

            wfApp.OnUnhandledException = delegate (System.Activities.WorkflowApplicationUnhandledExceptionEventArgs e)
            {
                hasError = true;
                isCompleted = true;
                state = "failed";
                Exception = e.UnhandledException;
                errormessage = e.UnhandledException.ToString();
                if(e.ExceptionSource!=null) errorsource = e.ExceptionSource.Id;
                //exceptionsource = e.ExceptionSource.Id;
                if (runWatch != null) runWatch.Stop();
                NotifyAborted();
                OnIdleOrComplete?.Invoke(this, EventArgs.Empty);
                return System.Activities.UnhandledExceptionAction.Terminate;
            };

        }
        private object filelock = new object();
        public void SaveFile()
        {
            if (string.IsNullOrEmpty(InstanceId)) return;
            if (string.IsNullOrEmpty(Path)) return;
            if (isCompleted || hasError) return;
            if (!System.IO.Directory.Exists(System.IO.Path.Combine(Path, "state"))) System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Path, "state"));
            var Filepath = System.IO.Path.Combine(Path, "state", InstanceId + ".json");
            lock(filelock)
            {
                System.IO.File.WriteAllText(Filepath, JsonConvert.SerializeObject(this));
            }
        }
        public void DeleteFile()
        {
            if (string.IsNullOrEmpty(InstanceId)) return;
            if (string.IsNullOrEmpty(Path)) return;
            var Filepath = System.IO.Path.Combine(Path, "state", InstanceId + ".json");
            try
            {
                if (System.IO.File.Exists(Filepath)) System.IO.File.Delete(Filepath);
            }
            catch (Exception ex)
            {
                Log.Debug(ex.ToString());
            }
        }
        private Task SaveTask = null;
        public void Save()
        {
            SaveFile();
            if(SaveTask==null)
            {
                //SaveTask = new Task.Delay(1000).ContinueWith(async () =>
                SaveTask = Task.Run(async () =>
                {
                    System.Threading.Thread.Sleep(1000);
                    try
                    {
                        if (isCompleted || hasError)
                        {
                            DeleteFile();
                        }
                        if (!global.isConnected) return;
                        //if ((DateTime.Now - LastUpdated).TotalMilliseconds < 2000) return;
                        //LastUpdated = DateTime.Now;
                        var result = await global.webSocketClient.InsertOrUpdateOne("openrpa_instances", 1, false, null, this);
                        _id = result._id;
                        _acl = result._acl;
                        //Log.Debug("Saved with id: " + _id);


                        // Catch up if others havent been saved
                        foreach (var i in Instances.ToList())
                        {
                            //if (string.IsNullOrEmpty(_id)) await i.Save();
                            if (string.IsNullOrEmpty(_id)) i.Save();
                        }

                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                        // throw;
                    }
                    finally
                    {
                        SaveTask = null;
                    }
                });
            }
        }
        public static async Task RunPendingInstances()
        {
            if (!global.isConnected) return;
            var host = Environment.MachineName.ToLower();
            var fqdn = System.Net.Dns.GetHostEntry(Environment.MachineName).HostName.ToLower();
            var results = await global.webSocketClient.Query<WorkflowInstance>("openrpa_instances", "{'$or':[{state: 'idle'}, {state: 'running'}], fqdn: '" + fqdn + "'}", top: 1000);
            foreach (var i in results)
            {
                try
                {
                    var workflow = MainWindow.instance.GetWorkflowByIDOrRelativeFilename(i.WorkflowId) as Workflow;
                    if (workflow == null) continue;
                    i.Workflow = workflow;
                    if (!string.IsNullOrEmpty(i.InstanceId) && string.IsNullOrEmpty(i.xml))
                    {
                        Log.Error("Refuse to load instance " + i.InstanceId + " it contains no state!");
                        i.state = "aborted";
                        i.errormessage = "Refuse to load instance " + i.InstanceId + " it contains no state!";
                        i.Save();
                        continue;
                    }
                    //if (idleOrComplete != null) i.OnIdleOrComplete += idleOrComplete;
                    //if (VisualTracking != null) i.OnVisualTracking += VisualTracking;
                    WorkflowInstance.Instances.Add(i);
                    var _ref = (i as IWorkflowInstance);
                    foreach (var runner in Plugins.runPlugins)
                    {
                        if (!runner.onWorkflowStarting(ref _ref, true)) throw new Exception("Runner plugin " + runner.Name + " declined running workflow instance");
                    }
                    i.createApp();
                    i.Run();
                }
                catch (Exception ex)
                {
                    i.state = "failed";
                    i.Exception = ex;
                    i.errormessage = ex.Message;
                    i.Save();
                    Log.Error("RunPendingInstances: " + ex.ToString());
                }
            }
        }
    }

}
