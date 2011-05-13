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
using System.Net;
using Erlang.NET;
using NUnit.Framework;
using Spring.Erlang.Connection;

namespace Spring.Erlang.Core
{
    [TestFixture]
    public class ErlangTemplateIntegrationTests
    {
        [Test]
        public void TestExecuteRpc()
        {
            string selfNodeName = "rabbit-monitor";
            string peerNodeName = "rabbit@" + Dns.GetHostName();

            var cf = new SimpleConnectionFactory(selfNodeName, peerNodeName);

            cf.AfterPropertiesSet();
            var template = new ErlangTemplate(cf);
            template.AfterPropertiesSet();

            var encoding = new System.Text.UTF8Encoding();
    
            var result = template.ExecuteRpc("rabbit_amqqueue", "info_all", encoding.GetBytes("/"));

            Console.WriteLine(result);
        }
    }
}