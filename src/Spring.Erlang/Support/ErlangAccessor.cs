
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
using Common.Logging;
using Erlang.NET;
using Spring.Erlang.Connection;
using Spring.Objects.Factory;
using Spring.Util;

namespace Spring.Erlang.Support
{
    /// <summary>
    /// An erlang accessor.
    /// </summary>
    /// <author>Mark Pollack</author>
    public abstract class ErlangAccessor : IInitializingObject
    {
        /// <summary>
        /// The logger.
        /// </summary>
        protected static readonly ILog logger = LogManager.GetLogger(typeof(ErlangAccessor));

        /// <summary>
        /// The connection factory.
        /// </summary>
        private IConnectionFactory connectionFactory;

        /// <summary>
        /// Gets or sets the connection factory.
        /// </summary>
        /// <value>The connection factory.</value>
        /// <remarks></remarks>
        public IConnectionFactory ConnectionFactory
        {
            get { return this.connectionFactory; }
            set { this.connectionFactory = value; }
        }

        /// <summary>
        /// Afters the properties set.
        /// </summary>
        /// <remarks></remarks>
        public void AfterPropertiesSet()
        {
            AssertUtils.ArgumentNotNull(this.ConnectionFactory, "ConnectionFactory is required");
        }

        /// <summary>
        /// Creates the connection.
        /// </summary>
        /// <returns>The connection.</returns>
        /// <remarks></remarks>
        protected IConnection CreateConnection()
        {
            return this.ConnectionFactory.CreateConnection();
        }
    }
}