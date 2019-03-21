﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FederatedExchangeParser.cs" company="The original author or authors.">
//   Copyright 2002-2012 the original author or authors.
//   
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
//   the License. You may obtain a copy of the License at
//   
//   https://www.apache.org/licenses/LICENSE-2.0
//   
//   Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
//   specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives
using System;
using System.Xml;
using RabbitMQ.Client.Exceptions;
using Spring.Messaging.Amqp.Core;
using Spring.Messaging.Amqp.Rabbit.Support;
using Spring.Objects.Factory.Support;
using Spring.Objects.Factory.Xml;
#endregion

namespace Spring.Messaging.Amqp.Rabbit.Config
{
    /// <summary>The federated exchange parser.</summary>
    public class FederatedExchangeParser : AbstractExchangeParser
    {
        private static readonly string BACKING_TYPE_ATTRIBUTE = "backing-type";

        private static readonly string UPSTREAM_SET_ATTRIBUTE = "upstream-set";

        private static readonly string DIRECT_BINDINGS_ELE = "direct-bindings";

        private static readonly string TOPIC_BINDINGS_ELE = "topic-bindings";

        private static readonly string TOPIC_FANOUT_ELE = "fanout-bindings";

        private static readonly string TOPIC_HEADERS_ELE = "headers-bindings";

        /// <summary>The get object type.</summary>
        /// <param name="element">The element.</param>
        /// <returns>The System.Type.</returns>
        protected override Type GetObjectType(XmlElement element) { return typeof(FederatedExchange); }

        /// <summary>The do parse.</summary>
        /// <param name="element">The element.</param>
        /// <param name="parserContext">The parser context.</param>
        /// <param name="builder">The builder.</param>
        protected override void DoParse(XmlElement element, ParserContext parserContext, ObjectDefinitionBuilder builder)
        {
            base.DoParse(element, parserContext, builder);
            NamespaceUtils.SetValueIfAttributeDefined(builder, element, BACKING_TYPE_ATTRIBUTE);
            NamespaceUtils.SetValueIfAttributeDefined(builder, element, UPSTREAM_SET_ATTRIBUTE);
        }

        /// <summary>The parse bindings.</summary>
        /// <param name="element">The element.</param>
        /// <param name="parserContext">The parser context.</param>
        /// <param name="builder">The builder.</param>
        /// <param name="exchangeName">The exchange name.</param>
        protected override void ParseBindings(XmlElement element, ParserContext parserContext, ObjectDefinitionBuilder builder, string exchangeName)
        {
            var backingType = element.GetAttribute(BACKING_TYPE_ATTRIBUTE);
            var bindings = element.SelectChildElementByTagName(DIRECT_BINDINGS_ELE);
            if (bindings != null && ExchangeTypes.Direct != backingType)
            {
                parserContext.ReaderContext.ReportFatalException(element, "Cannot have direct-bindings if backing-type not 'direct'");
            }

            if (bindings == null)
            {
                bindings = element.SelectChildElementByTagName(TOPIC_BINDINGS_ELE);
                if (bindings != null && !ExchangeTypes.Topic.Equals(backingType))
                {
                    parserContext.ReaderContext.ReportFatalException(element, "Cannot have topic-bindings if backing-type not 'topic'");
                }
            }

            if (bindings == null)
            {
                bindings = element.SelectChildElementByTagName(TOPIC_FANOUT_ELE);
                if (bindings != null && !ExchangeTypes.Fanout.Equals(backingType))
                {
                    parserContext.ReaderContext.ReportFatalException(element, "Cannot have fanout-bindings if backing-type not 'fanout'");
                }
            }

            if (bindings == null)
            {
                bindings = element.SelectChildElementByTagName(TOPIC_HEADERS_ELE);
                if (bindings != null && !ExchangeTypes.Headers.Equals(backingType))
                {
                    parserContext.ReaderContext.ReportFatalException(element, "Cannot have headers-bindings if backing-type not 'headers'");
                }
            }

            if (!string.IsNullOrWhiteSpace(backingType))
            {
                if (ExchangeTypes.Direct.Equals(backingType))
                {
                    this.DoParseBindings(parserContext, exchangeName, bindings, new DirectExchangeParser());
                }
                else if (ExchangeTypes.Topic.Equals(backingType))
                {
                    this.DoParseBindings(parserContext, exchangeName, bindings, new TopicExchangeParser());
                }
                else if (ExchangeTypes.Fanout.Equals(backingType))
                {
                    this.DoParseBindings(parserContext, exchangeName, bindings, new FanoutExchangeParser());
                }
                else if (ExchangeTypes.Headers.Equals(backingType))
                {
                    this.DoParseBindings(parserContext, exchangeName, bindings, new HeadersExchangeParser());
                }
            }
        }

        /// <summary>The parse binding.</summary>
        /// <param name="exchangeName">The exchange name.</param>
        /// <param name="binding">The binding.</param>
        /// <param name="parserContext">The parser context.</param>
        /// <returns>The Spring.Objects.Factory.Support.AbstractObjectDefinition.</returns>
        protected override AbstractObjectDefinition ParseBinding(string exchangeName, XmlElement binding, ParserContext parserContext) { throw new UnsupportedMethodException("Not supported for federated exchange"); }
    }
}
