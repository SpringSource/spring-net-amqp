﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Common.Logging;
using NUnit.Framework;
using RabbitMQ.Client;
using Spring.Messaging.Amqp.Core;
using Spring.Messaging.Amqp.Rabbit.Admin;
using Spring.Messaging.Amqp.Rabbit.Connection;
using Spring.Messaging.Amqp.Rabbit.Core;
using Spring.Messaging.Amqp.Rabbit.Listener.Adapter;
using Spring.Messaging.Amqp.Rabbit.Test;
using Spring.Threading;

namespace Spring.Messaging.Amqp.Rabbit.Listener
{
    public class MessageListenerTxSizeIntegrationTests : IntegrationTestBase
    {
        private static ILog logger = LogManager.GetLogger(typeof(MessageListenerTxSizeIntegrationTests));

        private Queue queue = new Queue("test.queue");

        private RabbitTemplate template = new RabbitTemplate();

        private int concurrentConsumers = 1;

        private int messageCount = 12;

        public int txSize = 4;

        private bool transactional = true;

        private SimpleMessageListenerContainer container;

        //@Rule
        //public Log4jLevelAdjuster logLevels = new Log4jLevelAdjuster(Level.DEBUG, RabbitTemplate.class,
        //		SimpleMessageListenerContainer.class, BlockingQueueConsumer.class);

        //@Rule
        public BrokerRunning brokerIsRunning;
        
        [SetUp]
        public void CreateConnectionFactory()
        {
            this.brokerIsRunning = BrokerRunning.IsRunningWithEmptyQueues(queue);
            var connectionFactory = new CachingConnectionFactory();
            connectionFactory.ChannelCacheSize = concurrentConsumers;
            connectionFactory.Port = BrokerTestUtils.GetPort();
            template.ConnectionFactory = connectionFactory;
        }

        [TearDown]
        public void Clear()
        {
            // Wait for broker communication to finish before trying to stop container
            Thread.Sleep(300);
            logger.Debug("Shutting down at end of test");
            if (container != null)
            {
                container.Shutdown();
            }
        }

        [Test]
        public void TestListenerTransactionalSunnyDay()
        {
            transactional = true;
            CountDownLatch latch = new CountDownLatch(messageCount);
            container = CreateContainer(new TxTestListener(latch, false, this));
            for (int i = 0; i < messageCount; i++)
            {
                template.ConvertAndSend(queue.Name, i + "foo");
            }
            int timeout = Math.Min(1 + messageCount / (4 * concurrentConsumers), 30);
            logger.Debug("Waiting for messages with timeout = " + timeout + " (s)");
            var waited = latch.Await(new TimeSpan(0, 0, 0, timeout));
            Assert.True(waited, "Timed out waiting for message");
            Assert.Null(template.ReceiveAndConvert(queue.Name));
        }

        [Test]
        public void TestListenerTransactionalFails()
        {
            this.transactional = true;
            var latch = new CountDownLatch(this.messageCount);
            this.container = this.CreateContainer(new TxTestListener(latch, true, this));
            for (var i = 0; i < this.txSize; i++)
            {
                this.template.ConvertAndSend(this.queue.Name, i + "foo");
            }

            var timeout = Math.Min(1 + this.messageCount / (4 * this.concurrentConsumers), 30);
            logger.Debug("Waiting for messages with timeout = " + timeout + " (s)");
            var waited = latch.Await(new TimeSpan(0, 0, 0, timeout));
            Assert.True(waited, "Timed out waiting for message");
            Assert.Null(this.template.ReceiveAndConvert(this.queue.Name));
        }

        private SimpleMessageListenerContainer CreateContainer(object listener)
        {
            var container = new SimpleMessageListenerContainer(this.template.ConnectionFactory);
            container.MessageListener = new MessageListenerAdapter(listener);
            container.QueueNames = new string[] { this.queue.Name };
            container.TxSize = this.txSize;
            container.PrefetchCount = this.txSize;
            container.ConcurrentConsumers = this.concurrentConsumers;
            container.IsChannelTransacted = this.transactional;
            container.AcknowledgeMode = AcknowledgeModeUtils.AcknowledgeMode.AUTO;
            container.AfterPropertiesSet();
            container.Start();
            return container;
        }
    }

    /// <summary>
    /// A Tx Test Listener
    /// </summary>
    /// <remarks></remarks>
    public class TxTestListener : IChannelAwareMessageListener
    {
        private static ILog logger = LogManager.GetLogger(typeof(TestListener));
        private ThreadLocal<int> count = new ThreadLocal<int>();
        private readonly MessageListenerTxSizeIntegrationTests outer;

        private readonly CountDownLatch latch;

        private readonly bool fail;

        /// <summary>
        /// Initializes a new instance of the <see cref="TxTestListener"/> class.
        /// </summary>
        /// <param name="latch">The latch.</param>
        /// <param name="fail">if set to <c>true</c> [fail].</param>
        /// <param name="outer">The outer.</param>
        /// <remarks></remarks>
        public TxTestListener(CountDownLatch latch, bool fail, MessageListenerTxSizeIntegrationTests outer)
        {
            this.latch = latch;
            this.fail = fail;
            this.outer = outer;
        }

        /// <summary>
        /// Handles the message.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <remarks></remarks>
        public void HandleMessage(string value)
        {
        }

        /// <summary>
        /// Called when [message].
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="channel">The channel.</param>
        /// <remarks></remarks>
        public void OnMessage(Message message, IModel channel)
        {
            var value = Encoding.UTF8.GetString(message.Body);
            try
            {
                logger.Debug("Received: " + value);
                if (this.count.Value == null)
                {
                    this.count.Value = 1;
                }
                else
                {
                    this.count.Value = this.count.Value + 1;
                }

                if (this.count.Value == this.outer.txSize && this.fail)
                {
                    logger.Debug("Failing: " + value);
                    this.count.Value = 0;
                    throw new SystemException("Planned");
                }
            }
            finally
            {
                this.latch.CountDown();
            }
        }
    }
}