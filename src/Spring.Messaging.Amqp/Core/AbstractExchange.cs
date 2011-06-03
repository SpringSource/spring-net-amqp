
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

using System.Collections;

namespace Spring.Messaging.Amqp.Core
{
    /// <summary>
    /// Common properties that describe all exchange types.  
    /// Subclasses of this class are typically used with administrative operations that declare an exchange.
    /// </summary>
    /// <author>Mark Pollack</author>
    /// <author>Joe Fitzgerald</author>
    public abstract class AbstractExchange : IExchange
    {
        /// <summary>
        /// The name.
        /// </summary>
        private readonly string name;

        /// <summary>
        /// The durable flag.
        /// </summary>
        private readonly bool durable;

        /// <summary>
        /// The auto delete flag.
        /// </summary>
        private readonly bool autoDelete;

        /// <summary>
        /// The arguments.
        /// </summary>
        private readonly IDictionary arguments;

        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractExchange"/> class, given a name.
        /// </summary>
        /// <param name="name">The name of the exchange.</param>
        public AbstractExchange(string name) : this(name, false, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractExchange"/> class, given a name, durability flag, auto-delete flag.
        /// </summary>
        /// <param name="name">
        /// The name of the exchange.
        /// </param>
        /// <param name="durable">
        /// The durable flag. <c>true</c> if we are declaring a durable exchange (the exchange will survive a server restart).
        /// </param>
        /// <param name="autoDelete">
        /// The auto delete flag. True if the server should delete the exchange when it is no longer in use.
        /// </param>
        public AbstractExchange(string name, bool durable, bool autoDelete) : this(name, durable, autoDelete, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractExchange"/> class, given a name, durability flag, and auto-delete flag. 
        /// </summary>
        /// <param name="name">The name of the exchange.</param>
        /// <param name="durable">if set to <c>true</c>, 
        /// if we are declaring a durable exchange (the exchange will survive a server restart)</param>
        /// <param name="autoDelete">if set to <c>true</c>
        /// the server should delete the exchange when it is no longer in use</param>
        public AbstractExchange(string name, bool durable, bool autoDelete, IDictionary arguments)
        {
            this.name = name;
            this.durable = durable;
            this.autoDelete = autoDelete;
            this.arguments = arguments;
        }

        public abstract string ExchangeType { get; }


        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="AbstractExchange"/> is durable.
        /// </summary>
        /// <value><c>true</c> if describing a durable exchange (the exchange will survive a server restart), 
        /// otherwise <c>false</c>.</value>
        public bool Durable
        {
            get { return durable; }
        }

        /// <summary>
        /// Gets or sets a value indicating the auto-delete lifecycle of this exchange.
        /// </summary>
        /// <value><c>true</c> if if the server should delete the exchange when it is no longer in use; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// An non-auto-deleted exchange lasts until the server is shut down.
        /// </remarks>
        public bool AutoDelete
        {
            get { return autoDelete; }
        }

        /// <summary>
        /// Gets or sets the collection of arbitrary arguments to use when declaring an exchange.
        /// </summary>
        /// <value>The arguments.</value>
        public IDictionary Arguments
        {
            get { return arguments; }
        }

        public override string ToString()
        {
            return string.Format("Name: {0}, Durable: {1}, AutoDelete: {2}, Arguments: {3}", this.GetType(), this.durable, this.autoDelete, this.arguments);
        }
    }

}