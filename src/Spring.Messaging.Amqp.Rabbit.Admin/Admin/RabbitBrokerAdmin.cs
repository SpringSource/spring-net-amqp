#region License

/*
 * Copyright 2002-2010 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Erlang.NET;
using RabbitMQ.Client;
using Spring.Erlang.Connection;
using Spring.Erlang.Core;
using Spring.Messaging.Amqp.Core;
using Spring.Messaging.Amqp.Rabbit.Core;
using Spring.Messaging.Amqp.Utils;
using Spring.Util;
using IConnectionFactory = Spring.Messaging.Amqp.Rabbit.Connection.IConnectionFactory;

namespace Spring.Messaging.Amqp.Rabbit.Admin
{
    /// <summary>
    /// Rabbit broker administration implementation
    /// </summary>
    /// <author>Mark Pollack</author>
    public class RabbitBrokerAdmin : IRabbitBrokerOperations
    {
        /// <summary>
        /// The default vhost.
        /// </summary>
        private static readonly string DEFAULT_VHOST = "/";

        /// <summary>
        /// The default node name.
        /// </summary>
        private static string DEFAULT_NODE_NAME = GetDefaultNodeName();

        /// <summary>
        /// The default port.
        /// </summary>
        private static readonly int DEFAULT_PORT = 5672;

        /// <summary>
        /// The default encoding.
        /// </summary>
        private static readonly string DEFAULT_ENCODING = "UTF-8";

        #region Logging Definition

        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILog logger = LogManager.GetLogger(typeof(RabbitBrokerAdmin));

        #endregion

        /// <summary>
        /// The erlang template.
        /// </summary>
        private ErlangTemplate erlangTemplate;

        private string encoding = DEFAULT_ENCODING;

        /// <summary>
        /// The timeout.
        /// </summary>
        private long timeout = 0;

        /// <summary>
        /// The executor.
        /// </summary>
        //private IExecutor executor;
        /// <summary>
        /// The node name.
        /// </summary>
        private readonly string nodeName;

        /// <summary>
        /// The cookie.
        /// </summary>
        private readonly string cookie;

        /// <summary>
        /// The port.
        /// </summary>
        private readonly int port;

        /// <summary>
        /// The rabbit log base directory.
        /// </summary>
        private string rabbitLogBaseDirectory;

        /// <summary>
        /// The rabbit mnesia base directory.
        /// </summary>
        private string rabbitMnesiaBaseDirectory;

        /// <summary>
        /// The module adapter.
        /// </summary>
        private IDictionary<String, String> moduleAdapter = new Dictionary<String, String>();

        /// <summary>
        /// Gets the default name of the node.
        /// </summary>
        /// <returns>The default node name.</returns>
        /// <remarks></remarks>
        private static string GetDefaultNodeName()
        {
            try
            {
                var hostName = Dns.GetHostName().ToUpper();
                return "rabbit@" + hostName;
            }
            catch (Exception e)
            {
                return "rabbit@LOCALHOST";
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        /// <remarks></remarks>
        public RabbitBrokerAdmin() : this(DEFAULT_NODE_NAME)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitBrokerAdmin"/> class.
        /// </summary>
        /// <param name="nodeName">Name of the node.</param>
        /// Create an instance by supplying the erlang node name (e.g. "rabbit@myserver"), or simply the hostname (if the
        /// alive name is "rabbit").
        /// @param nodeName the node name or hostname to use
        /// <remarks></remarks>
        public RabbitBrokerAdmin(string nodeName) : this(nodeName, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitBrokerAdmin"/> class.
        /// </summary>
        /// <param name="nodeName">Name of the node.</param>
        /// <param name="cookie">The cookie.</param>
        /// Create an instance by supplying the erlang node name and cookie (unique string).
        /// @param nodeName the node name or hostname to use
        /// @param cookie the cookie value to use
        /// <remarks></remarks>
        public RabbitBrokerAdmin(string nodeName, string cookie) : this(nodeName, DEFAULT_PORT, cookie)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitBrokerAdmin"/> class.
        /// </summary>
        /// <param name="nodeName">Name of the node.</param>
        /// <param name="port">The port.</param>
        /// Create an instance by supplying the erlang node name and port number. Use this on a UN*X system if you want to
        /// run the broker as a user without root privileges, supplying values that do not clash with the default broker
        /// (usually "rabbit@&lt;servername&gt;" and 5672). If, as well as managing an existing broker, you need to start the
        /// broker process, you will also need to set {@link #setRabbitLogBaseDirectory(String) RABBITMQ_LOG_BASE} and
        /// {@link #setRabbitMnesiaBaseDirectory(String) RABBITMQ_MNESIA_BASE} to point to writable directories).
        /// @param nodeName the node name or hostname to use
        /// @param port the port number (overriding the default which is 5672)
        /// <remarks></remarks>
        public RabbitBrokerAdmin(string nodeName, int port) : this(nodeName, port, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitBrokerAdmin"/> class.
        /// </summary>
        /// <param name="nodeName">Name of the node.</param>
        /// <param name="port">The port.</param>
        /// <param name="cookie">The cookie.</param>
        /// Create an instance by supplying the erlang node name, port number and cookie (unique string). If the node name
        /// does not contain an 
        /// <code>@</code>
        ///  character, it will be prepended with an alivename 
        /// <code>rabbit@</code>
        /// (interpreting the supplied value as just the hostname).
        /// @param nodeName the node name or hostname to use
        /// @param port the port number (overriding the default which is 5672)
        /// @param cookie the cookie value to use
        /// <remarks></remarks>
        public RabbitBrokerAdmin(string nodeName, int port, string cookie)
        {
            if (!nodeName.Contains("@"))
            {
                nodeName = "rabbit@" + nodeName; // it was just the host
            }

            var parts = nodeName.Split("@".ToCharArray());
            AssertUtils.State(parts.Length == 2, "The node name should be in the form alivename@host, e.g. rabbit@myserver");
            if (/*Os.isFamily("windows")*/ true && !DEFAULT_NODE_NAME.Equals(nodeName))
            {
                nodeName = parts[0] + "@" + parts[1].ToUpper();
            }

            this.port = port;
            this.cookie = cookie;
            this.nodeName = nodeName;

            // TODO: This doesn't look right... is it right?
            //var executor = Executors.NewCachedThreadPool();

            //executor.setDaemon(true);
            //this.executor = executor;
        }

        /// <summary>
        /// Sets the rabbit log base directory.
        /// </summary>
        /// <value>The rabbit log base directory.</value>
        /// The location of 
        /// <code>RABBITMQ_LOG_BASE</code>
        ///  to override the system default (which may be owned by another
        /// user). Only needed for launching the broker process. Can also be set as a system property.
        /// @param rabbitLogBaseDirectory the rabbit log base directory to set
        /// <remarks></remarks>
        public string RabbitLogBaseDirectory
        {
            set { this.rabbitLogBaseDirectory = value; }
        }

        /// <summary>
        /// Sets the rabbit mnesia base directory.
        /// </summary>
        /// <value>The rabbit mnesia base directory.</value>
        /// The location of 
        /// <code>RABBITMQ_MNESIA_BASE</code>
        ///  to override the system default (which may be owned by another
        /// user). Only needed for launching the broker process. Can also be set as a system property.
        /// @param rabbitMnesiaBaseDirectory the rabbit Mnesia base directory to set
        /// <remarks></remarks>
        public string RabbitMnesiaBaseDirectory
        {
            set { this.rabbitMnesiaBaseDirectory = value; }
        }

        /// <summary>
        /// Sets the encoding.
        /// </summary>
        /// <value>The encoding.</value>
        /// The encoding to use for converting host names to byte arrays (which is needed on the remote side).
        /// @param encoding the encoding to use (default UTF-8)
        /// <remarks></remarks>
        public string Encoding
        {
            set { this.encoding = value; }
        }

        /// <summary>
        /// Sets the startup timeout.
        /// </summary>
        /// <value>The startup timeout.</value>
        /// Timeout (milliseconds) to wait for the broker to come up. If the provided timeout is greater than zero then we
        /// wait for that period for the broker to be ready. If it is not ready after that time the process is stopped.
        /// Defaults to 0 (no wait).
        /// @param timeout the timeout value to set in milliseconds
        /// <remarks></remarks>
        public long StartupTimeout
        {
            set { this.timeout = value; }
        }

        /// <summary>
        /// Sets the module adapter.
        /// </summary>
        /// <value>The module adapter.</value>
        /// Allows users to adapt Erlang RPC
        /// <code>(module, function)</code>
        /// pairs to older, or different, versions of the
        /// broker than the current target. The map is from String to String in the form
        /// <code>input_module%input_function -&gt; output_module%output_function</code>
        /// (using a
        /// <code>%</code>
        /// separator).
        /// @param moduleAdapter the module adapter to set
        /// <remarks></remarks>
        public IDictionary<string, string> ModuleAdapter
        {
            set { this.moduleAdapter = value; }
        }

        /// <summary>
        /// Gets the queues.
        /// </summary>
        /// <returns>A list of queues.</returns>
        /// <remarks></remarks>
        public List<QueueInfo> GetQueues()
        {
            return this.ExecuteAndConvertRpc<List<QueueInfo>>("rabbit_amqqueue", "info_all", this.GetBytes(DEFAULT_VHOST));
        }

        /// <summary>
        /// Gets the queues.
        /// </summary>
        /// <param name="virtualHost">The virtual host.</param>
        /// <returns>A list of queues.</returns>
        /// <remarks></remarks>
        public List<QueueInfo> GetQueues(string virtualHost)
        {
            return this.ExecuteAndConvertRpc<List<QueueInfo>>("rabbit_amqqueue", "info_all", this.GetBytes(virtualHost));
        }

        // User management

        /// <summary>
        /// Adds the user.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <remarks></remarks>
        public void AddUser(string username, string password)
        {
            this.ExecuteAndConvertRpc<object>("rabbit_auth_backend_internal", "add_user", this.GetBytes(username), this.GetBytes(password));
        }

        /// <summary>
        /// Deletes the user.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <remarks></remarks>
        public void DeleteUser(string username)
        {
            this.ExecuteAndConvertRpc<object>("rabbit_auth_backend_internal", "delete_user", this.GetBytes(username));
        }

        /// <summary>
        /// Changes the user password.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="newPassword">The new password.</param>
        /// <remarks></remarks>
        public void ChangeUserPassword(string username, string newPassword)
        {
            this.ExecuteAndConvertRpc<object>("rabbit_auth_backend_internal", "change_password", this.GetBytes(username), this.GetBytes(newPassword));
        }

        /// <summary>
        /// Lists the users.
        /// </summary>
        /// <returns>A list of users.</returns>
        /// <remarks></remarks>
        public List<string> ListUsers()
        {
            return this.ExecuteAndConvertRpc<List<string>>("rabbit_auth_backend_internal", "list_users");
        }

        /// <summary>
        /// Adds the vhost.
        /// </summary>
        /// <param name="vhostPath">The vhost path.</param>
        /// <returns>The value.</returns>
        /// <remarks></remarks>
        public int AddVhost(string vhostPath)
        {
            // TODO Auto-generated method stub
            return 0;
        }

        /// <summary>
        /// Deletes the vhost.
        /// </summary>
        /// <param name="vhostPath">The vhost path.</param>
        /// <returns>The value.</returns>
        /// <remarks></remarks>
        public int DeleteVhost(string vhostPath)
        {
            // TODO Auto-generated method stub
            return 0;
        }

        /// <summary>
        /// Sets the permissions.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="configure">The configure.</param>
        /// <param name="read">The read.</param>
        /// <param name="write">The write.</param>
        /// <remarks></remarks>
        public void SetPermissions(string username, Regex configure, Regex read, Regex write)
        {
            // TODO Auto-generated method stub
        }

        /// <summary>
        /// Sets the permissions.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="configure">The configure.</param>
        /// <param name="read">The read.</param>
        /// <param name="write">The write.</param>
        /// <param name="vhostPath">The vhost path.</param>
        /// <remarks></remarks>
        public void SetPermissions(string username, Regex configure, Regex read, Regex write, string vhostPath)
        {
            // TODO Auto-generated method stub
        }

        /// <summary>
        /// Clears the permissions.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <remarks></remarks>
        public void ClearPermissions(string username)
        {
            // TODO Auto-generated method stub
        }

        /// <summary>
        /// Clears the permissions.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="vhostPath">The vhost path.</param>
        /// <remarks></remarks>
        public void ClearPermissions(string username, string vhostPath)
        {
            // TODO Auto-generated method stub
        }

        /// <summary>
        /// Lists the permissions.
        /// </summary>
        /// <returns>A list of permissions.</returns>
        /// <remarks></remarks>
        public IList<string> ListPermissions()
        {
            // TODO Auto-generated method stub
            return null;
        }

        /// <summary>
        /// Lists the permissions.
        /// </summary>
        /// <param name="vhostPath">The vhost path.</param>
        /// <returns>A list of permissions.</returns>
        /// <remarks></remarks>
        public IList<string> ListPermissions(string vhostPath)
        {
            // TODO Auto-generated method stub
            return null;
        }

        /// <summary>
        /// Lists the user permissions.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <returns>A list of user permissions.</returns>
        /// <remarks></remarks>
        public IList<string> ListUserPermissions(string username)
        {
            // TODO Auto-generated method stub
            return null;
        }

        /// <summary>
        /// Starts the broker application.
        /// </summary>
        /// Starts the RabbitMQ application on an already running node. This command is typically run after performing other
        /// management actions that required the RabbitMQ application to be stopped, e.g. reset.
        /// <remarks></remarks>
        public void StartBrokerApplication()
        {
            var status = this.GetStatus();
            if (status.IsReady)
            {
                this.logger.Info("Rabbit Application already running.");
                return;
            }

            if (!status.IsAlive)
            {
                this.logger.Info("Rabbit Process not running.");
                this.StartNode();
                return;
            }

            this.logger.Info("Starting Rabbit Application.");

            this.ExecuteAndConvertRpc<object>("rabbit", "start");
        }

        /// <summary>
        /// Stops the broker application.
        /// </summary>
        /// Stops the RabbitMQ application, leaving the Erlang node running.
        /// <remarks></remarks>
        public void StopBrokerApplication()
        {
            this.logger.Info("Stopping Rabbit Application.");
            this.ExecuteAndConvertRpc<object>("rabbit", "stop");
            if (timeout > 0)
            {
                this.WaitForUnreadyState();
            }
        }

        #region Alternate StartBrokerApplication Implementation - .NET 4.0 TPL

        /*public void StartBrokerApplication()
    {
        var status = this.Status;
        if (status.IsReady)
        {
            logger.Info("Rabbit Application already running.");
            return;
        }
        if (!status.IsAlive)
        {
            logger.Info("Rabbit Process not running.");
            this.StartNode();
            return;
        }
        logger.Info("Starting Rabbit Application.");


        using (var cancelTokenSource = new CancellationTokenSource())
        {
            var ct = cancelTokenSource.Token;

            // This call in particular seems to be prone to hanging, so do it in the background...
            using (var latch = new CountdownEvent(1))
            {
                var result = Task.Factory.StartNew(() =>
                                                       {
                                                           try
                                                           {
                                                               return this.ExecuteAndConvertRpc<object>("rabbit", "start");
                                                           }
                                                           finally
                                                           {
                                                               latch.Signal();
                                                           }
                                                       }, ct);
                var started = false;
                try
                {
                    result.Wait((int) timeout, ct);
                    started = latch.Wait(new TimeSpan(0, 0, 0, 0, (int) timeout), ct);
                }
                catch (AggregateException e)
                {
                    if (e.InnerException is OperationCanceledException)
                    {
                        this.logger.Debug("Task Cancelled");
                    }
                    return;
                }

                if (this.timeout > 0 && started)
                {
                    if (!this.WaitForReadyState() && !result.IsCompleted)
                    {
                        cancelTokenSource.Cancel();
                    }
                }
            }
        }
    }*/

        #endregion

        /// <summary>
        /// Starts the node.
        /// </summary>
        /// Starts the Erlang node where RabbitMQ is running by shelling out to the directory specified by RABBITMQ_HOME and
        /// executing the standard named start script. It spawns the shell command execution into its own thread.
        /// <remarks></remarks>
        public void StartNode()
        {
            var status = this.GetStatus();
            if (status.IsAlive)
            {
                this.logger.Info("Rabbit Process already running.");
                this.StartBrokerApplication();
                return;
            }

            if (!status.IsRunning && status.IsReady)
            {
                this.logger.Info("Rabbit Process not running but status is ready.  Restarting.");
                this.StopNode();
            }

            this.logger.Info("Starting RabbitMQ node by shelling out command line.");

            string rabbitStartScript = null;
            var hint = string.Empty;

            if (true /*Os.isFamily("windows") || Os.isFamily("dos")*/)
            {
                rabbitStartScript = @"sbin\rabbitmq-server.bat";
            }
            else if (false /*Os.isFamily("unix") || Os.isFamily("mac")*/)
            {
                rabbitStartScript = "bin/rabbitmq-server";
                hint =
                    "Depending on your platform it might help to set RABBITMQ_LOG_BASE and RABBITMQ_MNESIA_BASE System properties to an empty directory.";
            }

            AssertUtils.ArgumentNotNull(rabbitStartScript, "unsupported OS family");

            var rabbitHome = Environment.GetEnvironmentVariable("RABBITMQ_SERVER");
            if (rabbitHome == null)
            {
                if (true /*Os.isFamily("windows") || Os.isFamily("dos")*/)
                {
                    rabbitHome = this.FindDirectoryName(@"c:\Program Files (x86)\", "rabbitmq server");
                    rabbitHome = this.FindDirectoryName(rabbitHome, "rabbitmq_server-2.4.1");
                }
                else if (false /*Os.isFamily("unix") || Os.isFamily("mac")*/)
                {
                    rabbitHome = "/usr/lib/rabbitmq";
                }
            }

            AssertUtils.ArgumentNotNull(rabbitHome, "RABBITMQ_SERVER system property (or environment variable) not set.");

            // rabbitHome = StringUtils.cleanPath(rabbitHome);
            var rabbitStartCommand = rabbitHome + @"\" + rabbitStartScript;
            var execute = new ProcessStartInfo(rabbitStartCommand);

            if (this.rabbitLogBaseDirectory != null)
            {
                execute.EnvironmentVariables.Add("RABBITMQ_LOG_BASE", this.rabbitLogBaseDirectory);
            }
            else
            {
                this.AddEnvironment(execute.EnvironmentVariables, "RABBITMQ_LOG_BASE");
            }

            if (this.rabbitMnesiaBaseDirectory != null)
            {
                execute.EnvironmentVariables.Add("RABBITMQ_MNESIA_BASE", this.rabbitMnesiaBaseDirectory);
            }
            else
            {
                this.AddEnvironment(execute.EnvironmentVariables, "RABBITMQ_MNESIA_BASE");
            }

            this.AddEnvironment(execute.EnvironmentVariables, "ERLANG_HOME");

            // Make the nodename explicitly the same so the erl process knows who we are
            execute.EnvironmentVariables.Add("RABBITMQ_NODENAME", nodeName);

            // Set the port number for the new process
            execute.EnvironmentVariables.Add("RABBITMQ_NODE_PORT", port.ToString());

            // Ask for a detached erl process so stdout doesn't get diverted to a black hole when the JVM dies (without this
            // you can start the Rabbit broker form Java but if you forget to stop it, the erl process is hosed).
            execute.EnvironmentVariables.Add("RABBITMQ_SERVER_ERL_ARGS", "-detached");

            execute.UseShellExecute = false;
            execute.LoadUserProfile = true;

            //Process.Start(execute);

            var running = new CountdownEvent(1);
            var finished = false;
            var errorHint = hint;
            var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
            var task = Task.Factory.StartNew(
                                () =>
                               {
                                   try
                                   {
                                       running.Signal();
                                       var process = Process.Start(execute);
                                       process.WaitForExit();
                                       var exit = process.ExitCode;
                                       finished = true;
                                       logger.Info("Finished broker launcher process with exit code=" + exit);
                                       if (exit != 0)
                                       {
                                           throw new Exception("Could not start process." + errorHint);
                                       }
                                   }
                                   catch (Exception e)
                                   {
                                       logger.Error("Failed to start node", e);
                                   }
                               }, 
                               token);

            try 
            {
                this.logger.Info("Waiting for Rabbit process to be started");
                var result = task.Wait((int)this.timeout);
                AssertUtils.State(result, "Timed out waiting for thread to start Rabbit process.");
                if (!result)
                {
                    tokenSource.Cancel();
                }
                
            } 
            catch (Exception e) 
            {
                Thread.CurrentThread.Interrupt();
            }

            if (finished) 
            {
                //throw new Exception("Expected broker process to start in background, but it has exited early.");
            }

            if (this.timeout > 0) 
            {
                this.WaitForReadyState();
            }
        }

        /// <summary>
        /// A status callback delegate.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public delegate bool StatusCallbackDelegate(RabbitStatus status);

        /// <summary>
        /// Waits the state of for ready.
        /// </summary>
        /// <returns>The value.</returns>
        /// <remarks></remarks>
        private bool WaitForReadyState()
        {
            return this.WaitForState(delegate(RabbitStatus status) { return status.IsReady; }, "ready");
        }

        /// <summary>
        /// Waits the state of for unready.
        /// </summary>
        /// <returns>The value.</returns>
        /// <remarks></remarks>
        private bool WaitForUnreadyState()
        {
            return this.WaitForState(delegate(RabbitStatus status) { return !status.IsRunning; }, "unready");
        }

        /// <summary>
        /// Waits the state of for stopped.
        /// </summary>
        /// <returns>The value.</returns>
        /// <remarks></remarks>
        private bool WaitForStoppedState()
        {
            return this.WaitForState(delegate(RabbitStatus status) { return !status.IsReady && !status.IsRunning; }, "stopped");
        }

        /// <summary>
        /// Waits for state.
        /// </summary>
        /// <param name="callable">The callable.</param>
        /// <param name="state">The state.</param>
        /// <returns>The value.</returns>
        /// <remarks></remarks>
        private bool WaitForState(StatusCallbackDelegate callable, string state)
        {
            if (this.timeout <= 0)
            {
                return true;
            }

            var status = this.GetStatus();

            if (!callable.Invoke(status))
            {
                this.logger.Info("Waiting for broker to enter state: " + state);
                var tokenSource = new CancellationTokenSource();
                var token = tokenSource.Token;
                var started = Task.Factory.StartNew(
                                                    () =>
                                                        {
                                                            var internalstatus = GetStatus();
                                                            while (!callable.Invoke(internalstatus) && !token.IsCancellationRequested)
                                                            {
                                                                // Any less than 1000L and we tend to clog up the socket?
                                                                Thread.Sleep(500);
                                                                internalstatus = GetStatus();
                                                            }

                                                            return internalstatus;
                                                        }, 
                                                        token);

                try
                {
                    var result = started.Wait((int)this.timeout);
                    if (!result)
                    {
                        // tokenSource.Cancel();
                    }

                    status = started.Result;

                    // This seems to help... really it just means we didn't get the right status data
                    Thread.Sleep(500);
                }
                catch (Exception e)
                {
                    this.logger.Debug("error occurred waiting for result", e);
                }

                if (!callable.Invoke(status))
                {
                    this.logger.Error("Rabbit broker not in " + state + " state after timeout. Stopping process.");
                    this.StopNode();
                    return false;
                }
                else
                {
                    this.logger.Info("Finished waiting for broker to enter state: " + state);
                    if (this.logger.IsDebugEnabled)
                    {
                        this.logger.Info("Status: " + status);
                    }

                    return true;
                }
            }
            else
            {
                this.logger.Info("Broker already in state: " + state);
            }

            return true;
        }

        /// <summary>
        /// Finds the name of the directory.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="child">The child.</param>
        /// <returns></returns>
        /// Find a directory whose name starts with a substring in a given parent directory. If there is none return null,
        /// otherwise sort the results and return the best match (an exact match if there is one or the last one in a lexical
        /// sort).
        /// @param parent
        /// @param child
        /// @return the full name of a directory
        /// <remarks></remarks>
        private string FindDirectoryName(string parent, string child)
        {
            var parentDirectory = new DirectoryInfo(parent);
            if (parentDirectory.Exists)
            {
                var exactMatches = parentDirectory.GetDirectories(child, SearchOption.TopDirectoryOnly);
                if (exactMatches.Length == 1)
                {
                    return exactMatches[0].FullName;
                }

                var startMatches = parentDirectory.GetDirectories(child + "*", SearchOption.TopDirectoryOnly);
                return (from s in startMatches
                        orderby s.Name descending
                        select s.FullName).FirstOrDefault();
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Adds the environment.
        /// </summary>
        /// <param name="env">The env.</param>
        /// <param name="key">The key.</param>
        /// <remarks></remarks>
        private void AddEnvironment(StringDictionary env, string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (value != null)
            {
                if (!env.ContainsKey(key))
                {
                    this.logger.Debug("Adding environment variable: " + key + "=" + value);
                    env.Add(key, value);
                }
            }
        }

        /// <summary>
        /// Stops the node.
        /// </summary>
        /// Stops the halts the Erlang node on which RabbitMQ is running. To restart the node you will need to execute the
        /// start script from a command line or via other means.
        /// <remarks></remarks>
        public void StopNode()
        {
            this.logger.Info("Stopping RabbitMQ node.");
            try
            {
                this.ExecuteAndConvertRpc<object>("rabbit", "stop_and_halt");
            }
            catch (Exception e)
            {
                this.logger.Error("Failed to send stop signal", e);
            }

            if (timeout >= 0) 
            {
                this.WaitForStoppedState();
            }
        }

        /// <summary>
        /// Resets the node.
        /// Removes the node from any cluster it belongs to, removes all data from the management database, such as
        /// configured users and vhosts, and deletes all persistent messages.
        /// </summary>
        /// <remarks></remarks>
        public void ResetNode()
        {
            this.ExecuteAndConvertRpc<object>("rabbit_mnesia", "reset");
        }

        /// <summary>
        /// Forces the reset node.
        /// </summary>
        /// The forceResetNode command differs from {@link #resetNode} in that it resets the node unconditionally, regardless
        /// of the current management database state and cluster configuration. It should only be used as a last resort if
        /// the database or cluster configuration has been corrupted.
        /// <remarks></remarks>
        public void ForceResetNode()
        {
            this.ExecuteAndConvertRpc<object>("rabbit_mnesia", "force_reset");
        }

        public RabbitStatus GetStatus()
        {
            try
            {
                return this.ExecuteAndConvertRpc<RabbitStatus>("rabbit", "status");
            }
            catch (OtpAuthException e)
            {
                throw new RabbitAdminAuthException(
                    "Could not authorise connection to Erlang process. This can happen if the broker is running, "
                    + "but as root or rabbitmq and the current user is not authorised to connect. Try starting the "
                    + "broker again as a different user.", e);
            }
            catch (OtpException e)
            {
                logger.Debug("Ignoring OtpException (assuming that the broker is simply not running)");
                if (logger.IsTraceEnabled)
                {
                    logger.Trace("Status not available owing to exception", e);
                }
                return new RabbitStatus(new List<Application>(), new List<Node>(), new List<Node>());
            }
            catch (Exception e)
            {
                logger.Debug("Error occurred getting status", e);
                throw;
            }
        }

        /// <summary>
        /// Initializes the default erlang template.
        /// </summary>
        /// <remarks></remarks>
        protected void InitializeDefaultErlangTemplate()
        {
            var peerNodeName = this.nodeName;
            this.logger.Debug("Creating connection with peerNodeName = [" + peerNodeName + "]");
            var otpConnectionFactory = new SimpleConnectionFactory("rabbit-spring-monitor", peerNodeName, this.cookie);
            otpConnectionFactory.AfterPropertiesSet();
            this.CreateErlangTemplate(otpConnectionFactory);
        }

        /// <summary>
        /// Creates the erlang template.
        /// </summary>
        /// <param name="otpConnectionFactory">The otp connection factory.</param>
        /// <remarks></remarks>
        protected void CreateErlangTemplate(Spring.Erlang.Connection.IConnectionFactory otpConnectionFactory)
        {
            this.erlangTemplate = new ErlangTemplate(otpConnectionFactory);
            this.erlangTemplate.ErlangConverter = new RabbitControlErlangConverter(this.moduleAdapter);
            this.erlangTemplate.AfterPropertiesSet();
        }

        /// <summary>
        /// Convenience method for lazy initialization of the {@link ErlangTemplate} and associated trimmings. All RPC calls should go through this method.
        /// </summary>
        /// <typeparam name="T">The type of result.</typeparam>
        /// <param name="module">The module to address remotely.</param>
        /// <param name="function">The function to call.</param>
        /// <param name="args">The arguments to pass.</param>
        /// <returns>The result from the remote erl process converted to the correct type</returns>
        /// <remarks></remarks>
        private T ExecuteAndConvertRpc<T>(string module, string function, params object[] args)
        {
            if (this.erlangTemplate == null)
            {
                lock (this)
                {
                    if (this.erlangTemplate == null)
                    {
                        this.InitializeDefaultErlangTemplate();
                    }
                }
            }

            var key = module + "%" + function;
            if (this.moduleAdapter.ContainsKey(key))
            {
                var adapter = this.moduleAdapter[key];
                var values = adapter.Split("%".ToCharArray());
                AssertUtils.State(values.Length == 2, "The module adapter should be a map from 'module%function' to 'module%function'. " + "This one contained [" + adapter + "] which cannot be parsed to a module, function pair.");
                module = values[0];
                function = values[1];
            }

            return (T)this.erlangTemplate.ExecuteAndConvertRpc(module, function, args);
        }

        /// <summary>
        /// Gets the bytes.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The byte representation of the string.</returns>
        /// Safely convert a string to its bytes using the encoding provided.
        /// @see #setEncoding(String)
        /// @param string the value to convert
        /// @return the bytes from the string using the encoding provided
        /// @throws IllegalStateException if the encoding is ont supported
        /// <remarks></remarks>
        private byte[] GetBytes(string value)
        {
            try
            {
                return SerializationUtils.SerializeString(value, this.encoding);
            }
            catch (Exception e)
            {
                throw new Exception("Unsupported encoding: " + this.encoding);
            }
        }
    }

    /// <summary>
    /// A status callback interface.
    /// </summary>
    /// <remarks></remarks>
    internal interface IStatusCallback
    {
        /// <summary>
        /// Gets the specified status.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns>Value indicating status.</returns>
        /// <remarks></remarks>
        bool Get(RabbitStatus status);
    }
}