﻿
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
using System.Collections;

namespace Spring.Messaging.Amqp.Core
{

    /// <summary>
    /// An anonymous queue.
    /// </summary>
    /// <author>Joe Fitzgerald</author>
    public class AnonymousQueue : Queue 
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AnonymousQueue"/> class.
        /// </summary>
        public AnonymousQueue() : base(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AnonymousQueue"/> class.
        /// </summary>
        /// <param name="arguments">
        /// The arguments.
        /// </param>
        public AnonymousQueue(IDictionary arguments) : base(Guid.NewGuid().ToString(), false, true, true, arguments)
        {
        }
    }
}

