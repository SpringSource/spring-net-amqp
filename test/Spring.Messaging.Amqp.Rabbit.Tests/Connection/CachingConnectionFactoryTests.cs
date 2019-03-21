// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CachingConnectionFactoryTests.cs" company="The original author or authors.">
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
using System.Collections.Generic;
using Common.Logging;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;
using Spring.Messaging.Amqp.Rabbit.Connection;
using Spring.Messaging.Amqp.Rabbit.Tests.Support;
using Spring.Messaging.Amqp.Rabbit.Tests.Test;
using Spring.Messaging.Amqp.Rabbit.Threading.AtomicTypes;
using Spring.Util;
using IConnection = RabbitMQ.Client.IConnection;
#endregion

namespace Spring.Messaging.Amqp.Rabbit.Tests.Connection
{
    /// <summary>
    /// Tests for the caching connection factory.
    /// </summary>
    [TestFixture]
    [Category(TestCategory.Unit)]
    public class CachingConnectionFactoryTests : AbstractConnectionFactoryTests
    {
        private static readonly ILog Logger = LogManager.GetCurrentClassLogger();

        /// <summary>Creates the connection factory.</summary>
        /// <param name="connectionFactory">The connection factory.</param>
        /// <returns>The created connection factory.</returns>
        protected override AbstractConnectionFactory CreateConnectionFactory(ConnectionFactory connectionFactory) { return new CachingConnectionFactory(connectionFactory); }

        /// <summary>
        /// Tests the with connection factory defaults.
        /// </summary>
        [Test]
        public void TestWithConnectionFactoryDefaults()
        {
            var mockConnectionFactory = new Mock<ConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IModel>();

            mockConnectionFactory.Setup(factory => factory.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(connection => connection.CreateModel()).Returns(mockChannel.Object);
            mockChannel.Setup(chan => chan.IsOpen).Returns(true);
            mockConnection.Setup(conn => conn.IsOpen).Returns(true);

            var ccf = new CachingConnectionFactory(mockConnectionFactory.Object);
            var con = ccf.CreateConnection();

            var channel = con.CreateChannel(false);
            channel.Close(); // should be ignored, and placed into channel cache.
            con.Close(); // should be ignored

            var con2 = ccf.CreateConnection();

            /*
             * will retrieve same channel object that was just put into channel cache
             */
            var channel2 = con2.CreateChannel(false);
            channel2.Close(); // should be ignored
            con2.Close(); // should be ignored

            Assert.AreSame(con, con2);
            Assert.AreSame(channel, channel2);
            mockConnection.Verify(conn => conn.Close(), Times.Never());
            mockChannel.Verify(chan => chan.Close(), Times.Never());
        }

        /// <summary>
        /// Tests the size of the connection factory cache.
        /// </summary>
        [Test]
        public void TestWithConnectionFactoryCacheSize()
        {
            var mockConnectionFactory = new Mock<ConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel1 = new Mock<IModel>();
            var mockChannel2 = new Mock<IModel>();

            mockConnectionFactory.Setup(a => a.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(a => a.IsOpen).Returns(true);
            mockConnection.Setup(a => a.CreateModel()).ReturnsInOrder(mockChannel1.Object, mockChannel2.Object);

            mockChannel1.Setup(a => a.BasicGet("foo", false)).Returns(new BasicGetResult(0, false, null, null, 1, null, null));
            mockChannel2.Setup(a => a.BasicGet("bar", false)).Returns(new BasicGetResult(0, false, null, null, 1, null, null));
            mockChannel1.Setup(a => a.IsOpen).Returns(true);
            mockChannel2.Setup(a => a.IsOpen).Returns(true);

            var ccf = new CachingConnectionFactory(mockConnectionFactory.Object);
            ccf.ChannelCacheSize = 2;

            var con = ccf.CreateConnection();

            var channel1 = con.CreateChannel(false);
            var channel2 = con.CreateChannel(false);

            channel1.BasicGet("foo", true);
            channel2.BasicGet("bar", true);

            channel1.Close(); // should be ignored, and add last into channel cache.
            channel2.Close(); // should be ignored, and add last into channel cache.

            var ch1 = con.CreateChannel(false); // remove first entry in cache (channel1)
            var ch2 = con.CreateChannel(false); // remove first entry in cache (channel2)

            Assert.AreNotSame(ch1, ch2);
            Assert.AreSame(ch1, channel1);
            Assert.AreSame(ch2, channel2);

            ch1.Close();
            ch2.Close();

            mockConnection.Verify(conn => conn.CreateModel(), Times.Exactly(2));

            con.Close(); // should be ignored

            mockConnection.Verify(c => c.Close(), Times.Never());
            mockChannel1.Verify(c => c.Close(), Times.Never());
            mockChannel2.Verify(c => c.Close(), Times.Never());
        }

        /// <summary>
        /// Tests the cache size exceeded.
        /// </summary>
        [Test]
        public void TestCacheSizeExceeded()
        {
            var mockConnectionFactory = new Mock<ConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel1 = new Mock<IModel>();
            var mockChannel2 = new Mock<IModel>();
            var mockChannel3 = new Mock<IModel>();

            mockConnectionFactory.Setup(c => c.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).ReturnsInOrder(mockChannel1.Object, mockChannel2.Object, mockChannel3.Object);
            mockConnection.Setup(c => c.IsOpen).Returns(true);

            // Called during physical close
            mockChannel1.Setup(c => c.IsOpen).Returns(true);
            mockChannel2.Setup(c => c.IsOpen).Returns(true);
            mockChannel3.Setup(c => c.IsOpen).Returns(true);

            var ccf = new CachingConnectionFactory(mockConnectionFactory.Object);
            ccf.ChannelCacheSize = 1;

            var con = ccf.CreateConnection();

            var channel1 = con.CreateChannel(false);

            // cache size is 1, but the other connection is not released yet so this creates a new one
            var channel2 = con.CreateChannel(false);
            Assert.AreNotSame(channel1, channel2);

            // should be ignored, and added last into channel cache.
            channel1.Close();

            // should be physically closed
            channel2.Close();

            // remove first entry in cache (channel1)
            var ch1 = con.CreateChannel(false);

            // create a new channel
            var ch2 = con.CreateChannel(false);

            Assert.AreNotSame(ch1, ch2);
            Assert.AreSame(ch1, channel1);
            Assert.AreNotSame(ch2, channel2);

            ch1.Close();
            ch2.Close();

            mockConnection.Verify(c => c.CreateModel(), Times.Exactly(3));

            con.Close(); // should be ignored

            mockConnection.Verify(c => c.Close(), Times.Never());
            mockChannel1.Verify(c => c.Close(), Times.Never());
            mockChannel2.Verify(c => c.Close(), Times.AtLeastOnce());
            mockChannel3.Verify(c => c.Close(), Times.AtLeastOnce());
        }

        /// <summary>
        /// Tests the cache size exceeded after close.
        /// </summary>
        [Test]
        public void TestCacheSizeExceededAfterClose()
        {
            var mockConnectionFactory = new Mock<ConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel1 = new Mock<IModel>();
            var mockChannel2 = new Mock<IModel>();

            mockConnectionFactory.Setup(c => c.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).ReturnsInOrder(mockChannel1.Object, mockChannel2.Object);
            mockConnection.Setup(c => c.IsOpen).Returns(true);

            // Called during physical close
            mockChannel1.Setup(c => c.IsOpen).Returns(true);
            mockChannel2.Setup(c => c.IsOpen).Returns(true);

            var ccf = new CachingConnectionFactory(mockConnectionFactory.Object);
            ccf.ChannelCacheSize = 1;

            var con = ccf.CreateConnection();

            var channel1 = con.CreateChannel(false);
            channel1.Close(); // should be ignored, and add last into channel cache.
            var channel2 = con.CreateChannel(false);
            channel2.Close(); // should be ignored, and add last into channel cache.
            Assert.AreSame(channel1, channel2);

            var ch1 = con.CreateChannel(false); // remove first entry in cache (channel1)
            var ch2 = con.CreateChannel(false); // create new channel

            Assert.AreNotSame(ch1, ch2);
            Assert.AreSame(ch1, channel1);
            Assert.AreNotSame(ch2, channel2);

            ch1.Close();
            ch2.Close();

            mockConnection.Verify(c => c.CreateModel(), Times.Exactly(2));

            con.Close(); // should be ignored

            mockConnection.Verify(c => c.Close(), Times.Never());
            mockChannel1.Verify(c => c.Close(), Times.Never());
            mockChannel2.Verify(c => c.Close(), Times.AtLeastOnce());
        }

        /// <summary>
        /// Tests the transactional and non transactional channels segregated.
        /// </summary>
        [Test]
        public void TestTransactionalAndNonTransactionalChannelsSegregated()
        {
            var mockConnectionFactory = new Mock<ConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel1 = new Mock<IModel>();
            var mockChannel2 = new Mock<IModel>();

            mockConnectionFactory.Setup(c => c.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).ReturnsInOrder(mockChannel1.Object, mockChannel2.Object);
            mockConnection.Setup(c => c.IsOpen).Returns(true);

            // Called during physical close
            mockChannel1.Setup(c => c.IsOpen).Returns(true);
            mockChannel2.Setup(c => c.IsOpen).Returns(true);

            var ccf = new CachingConnectionFactory(mockConnectionFactory.Object);
            ccf.ChannelCacheSize = 1;

            var con = ccf.CreateConnection();

            var channel1 = con.CreateChannel(true);
            channel1.TxSelect();
            channel1.Close(); // should be ignored, and add last into channel cache.

            /*
             * When a channel is created as non-transactional we should create a new one.
             */
            var channel2 = con.CreateChannel(false);
            channel2.Close(); // should be ignored, and add last into channel cache.
            Assert.AreNotSame(channel1, channel2);

            var ch1 = con.CreateChannel(true); // remove first entry in cache (channel1)
            var ch2 = con.CreateChannel(false); // create new channel

            Assert.AreNotSame(ch1, ch2);
            Assert.AreSame(ch1, channel1); // The non-transactional one
            Assert.AreSame(ch2, channel2);

            ch1.Close();
            ch2.Close();

            mockConnection.Verify(c => c.CreateModel(), Times.Exactly(2));

            con.Close(); // should be ignored

            mockConnection.Verify(c => c.Close(), Times.Never());
            mockChannel1.Verify(c => c.Close(), Times.Never());
            mockChannel2.Verify(c => c.Close(), Times.Never());

            var notxlist = (LinkedList<IChannelProxy>)ReflectionUtils.GetInstanceFieldValue(ccf, "cachedChannelsNonTransactional");
            Assert.AreEqual(1, notxlist.Count);

            var txlist = (LinkedList<IChannelProxy>)ReflectionUtils.GetInstanceFieldValue(ccf, "cachedChannelsTransactional");
            Assert.AreEqual(1, txlist.Count);
        }

        /// <summary>
        /// Tests the with connection factory destroy.
        /// </summary>
        [Test]
        public void TestWithConnectionFactoryDestroy()
        {
            var mockConnectionFactory = new Mock<ConnectionFactory>();
            var mockConnection = new Mock<IConnection>();

            var mockChannel1 = new Mock<IModel>();
            mockChannel1.Setup(m => m.GetHashCode()).Returns(1);
            var mockChannel2 = new Mock<IModel>();
            mockChannel1.Setup(m => m.GetHashCode()).Returns(2);

            Logger.Debug(m => m("Channel1 Hashcode: {0}", mockChannel1.Object.GetHashCode()));
            Logger.Debug(m => m("Channel2 Hashcode: {0}", mockChannel2.Object.GetHashCode()));
            Assert.AreNotSame(mockChannel1, mockChannel2);

            mockConnectionFactory.Setup(c => c.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).ReturnsInOrder(mockChannel1.Object, mockChannel2.Object);
            mockConnection.Setup(c => c.IsOpen).Returns(true);

            // Called during physical close
            mockChannel1.Setup(c => c.IsOpen).Returns(true);
            mockChannel2.Setup(c => c.IsOpen).Returns(true);

            var ccf = new CachingConnectionFactory(mockConnectionFactory.Object);
            ccf.ChannelCacheSize = 2;

            var con = ccf.CreateConnection();

            // This will return a proxy that surpresses calls to close
            var channel1 = con.CreateChannel(false);
            var channel2 = con.CreateChannel(false);

            // Should be ignored, and add last into channel cache.
            channel1.Close();
            channel2.Close();

            // remove first entry in cache (channel1)
            var ch1 = con.CreateChannel(false);

            // remove first entry in cache (channel2)
            var ch2 = con.CreateChannel(false);

            Assert.AreSame(ch1, channel1);
            Assert.AreSame(ch2, channel2);

            var target1 = ((IChannelProxy)ch1).GetTargetChannel();
            var target2 = ((IChannelProxy)ch2).GetTargetChannel();

            // make sure Moq returned different mocks for the channel
            Assert.AreNotSame(target1, target2);

            ch1.Close();
            ch2.Close();
            con.Close(); // should be ignored

            ccf.Dispose(); // should call close on connection and channels in cache

            mockConnection.Verify(c => c.CreateModel(), Times.Exactly(2));

            mockConnection.Verify(c => c.Close(), Times.Exactly(1));

            // verify(mockChannel1).close();
            mockChannel2.Verify(c => c.Close(), Times.Exactly(1));

            // After destroy we can get a new connection
            var con1 = ccf.CreateConnection();
            Assert.AreNotSame(con, con1);

            // This will return a proxy that surpresses calls to close
            var channel3 = con.CreateChannel(false);
            Assert.AreNotSame(channel3, channel1);
            Assert.AreNotSame(channel3, channel2);
        }

        /// <summary>
        /// Tests the with listener.
        /// </summary>
        [Test]
        public void TestWithChannelListener()
        {
            var mockConnectionFactory = new Mock<ConnectionFactory>();
            var mockConnection = new Mock<IConnection>();
            var mockChannel = new Mock<IModel>();

            mockConnectionFactory.Setup(c => c.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(m => m.IsOpen).Returns(true);
            mockChannel.Setup(m => m.IsOpen).Returns(true);
            mockConnection.Setup(m => m.CreateModel()).Returns(mockChannel.Object);

            var called = new AtomicInteger(0);
            var connectionFactory = this.CreateConnectionFactory(mockConnectionFactory.Object);

            var mockConnectionListener = new Mock<IConnectionListener>();
            mockConnectionListener.Setup(m => m.OnCreate(It.IsAny<Rabbit.Connection.IConnection>())).Callback((Rabbit.Connection.IConnection conn) => called.IncrementValueAndReturn());
            mockConnectionListener.Setup(m => m.OnClose(It.IsAny<Rabbit.Connection.IConnection>())).Callback((Rabbit.Connection.IConnection conn) => called.DecrementValueAndReturn());

            connectionFactory.ConnectionListeners = new List<IConnectionListener> { mockConnectionListener.Object };
            ((CachingConnectionFactory)connectionFactory).ChannelCacheSize = 1;

            var con = connectionFactory.CreateConnection();
            var channel = con.CreateChannel(false);
            Assert.AreEqual(1, called.Value);
            channel.Close();

            con.Close();
            mockConnection.Verify(c => c.Close(), Times.Never());

            connectionFactory.CreateConnection();
            con.CreateChannel(false);
            Assert.AreEqual(1, called.Value);

            connectionFactory.Dispose();
            mockConnection.Verify(c => c.Close(), Times.AtLeastOnce());

            mockConnectionFactory.Verify(c => c.CreateConnection(), Times.Exactly(1));
        }
    }
}
