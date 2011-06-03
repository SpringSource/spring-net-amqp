﻿
using System;
using System.Data;
using Common.Logging;
using Spring.Messaging.Amqp.Rabbit.Connection;
using Spring.Objects.Factory;
using Spring.Transaction;
using Spring.Transaction.Support;

namespace Spring.Messaging.Amqp.Rabbit.Transaction
{
    /**
 * {@link org.springframework.transaction.PlatformTransactionManager} implementation for a single Rabbit
 * {@link ConnectionFactory}. Binds a Rabbit Channel from the specified ConnectionFactory to the thread, potentially
 * allowing for one thread-bound channel per ConnectionFactory.
 * 
 * <p>
 * This local strategy is an alternative to executing Rabbit operations within, and synchronized with, external
 * transactions. This strategy is <i>not</i> able to provide XA transactions, for example in order to share transactions
 * between messaging and database access.
 * 
 * <p>
 * Application code is required to retrieve the transactional Rabbit resources via
 * {@link ConnectionFactoryUtils#getTransactionalResourceHolder(ConnectionFactory, boolean)} instead of a standard
 * {@link Connection#createChannel()} call with subsequent Channel creation. Spring's {@link RabbitTemplate} will
 * autodetect a thread-bound Channel and automatically participate in it.
 * 
 * <p>
 * <b>The use of {@link CachingConnectionFactory} as a target for this transaction manager is strongly recommended.</b>
 * CachingConnectionFactory uses a single Rabbit Connection for all Rabbit access in order to avoid the overhead of
 * repeated Connection creation, as well as maintaining a cache of Channels. Each transaction will then share the same
 * Rabbit Connection, while still using its own individual Rabbit Channel.
 * 
 * <p>
 * Transaction synchronization is turned off by default, as this manager might be used alongside a datastore-based
 * Spring transaction manager such as the JDBC {@link org.springframework.jdbc.datasource.DataSourceTransactionManager},
 * which has stronger needs for synchronization.
 * 
 * @author Dave Syer
 */

    /// <summary>
    /// A rabbit transaction manager.
    /// </summary>
    public class RabbitTransactionManager : AbstractPlatformTransactionManager, IResourceTransactionManager, IInitializingObject
    {
        #region Logging

        /// <summary>
        /// The logger.
        /// </summary>
        private readonly ILog logger = LogManager.GetLogger(typeof(RabbitTransactionManager));

        #endregion

        /// <summary>
        /// The connection factory.
        /// </summary>
        private IConnectionFactory connectionFactory;

        /**
         * Create a new RabbitTransactionManager for bean-style usage.
         * <p>
         * Note: The ConnectionFactory has to be set before using the instance. This constructor can be used to prepare a
         * RabbitTemplate via a BeanFactory, typically setting the ConnectionFactory via setConnectionFactory.
         * <p>
         * Turns off transaction synchronization by default, as this manager might be used alongside a datastore-based
         * Spring transaction manager like DataSourceTransactionManager, which has stronger needs for synchronization. Only
         * one manager is allowed to drive synchronization at any point of time.
         * @see #setConnectionFactory
         * @see #setTransactionSynchronization
         */

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitTransactionManager"/> class.
        /// </summary>
        public RabbitTransactionManager()
        {
            this.TransactionSynchronization = TransactionSynchronizationState.Never;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitTransactionManager"/> class.
        /// </summary>
        /// <param name="connectionFactory">
        /// The connection factory.
        /// </param>
        public RabbitTransactionManager(IConnectionFactory connectionFactory) : this()
        {
            this.connectionFactory = connectionFactory;
            this.AfterPropertiesSet();
        }

        /// <summary>
        /// Gets or sets ConnectionFactory.
        /// </summary>
        public IConnectionFactory ConnectionFactory
        {
            get { return this.connectionFactory; }
            set { this.connectionFactory = value; }
        }

        /// <summary>
        /// Actions to perform after properties are set. Make sure the ConnectionFactory has been set.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// </exception>
        public void AfterPropertiesSet()
        {
            if (this.ConnectionFactory == null)
            {
                throw new ArgumentException("Property 'connectionFactory' is required");
            }
        }

        /// <summary>
        /// Gets ResourceFactory.
        /// </summary>
        public object ResourceFactory
        {
            get { return this.ConnectionFactory; }
        }

        /// <summary>
        /// Get the transaction.
        /// </summary>
        /// <returns>
        /// The transaction.
        /// </returns>
        protected override object DoGetTransaction()
        {
            var txObject = new RabbitTransactionObject();
            txObject.ResourceHolder = (RabbitResourceHolder)TransactionSynchronizationManager.GetResource(this.ConnectionFactory);
            return txObject;
        }

        /// <summary>
        /// Determines if the supplied object is an existing transaction.
        /// </summary>
        /// <param name="transaction">
        /// The transaction.
        /// </param>
        /// <returns>
        /// True if the object is an existing transaction, else false.
        /// </returns>
        protected override bool IsExistingTransaction(object transaction)
        {
            var txObject = (RabbitTransactionObject)transaction;
            return txObject.ResourceHolder != null;
        }

        /// <summary>
        /// Do begin.
        /// </summary>
        /// <param name="transaction">
        /// The transaction.
        /// </param>
        /// <param name="definition">
        /// The definition.
        /// </param>
        /// <exception cref="InvalidIsolationLevelException">
        /// </exception>
        /// <exception cref="CannotCreateTransactionException">
        /// </exception>
        protected override void DoBegin(object transaction, ITransactionDefinition definition)
        {
            // TODO: Figure out the right isolation level.
            if (definition.TransactionIsolationLevel != IsolationLevel.Unspecified)
            {
                throw new InvalidIsolationLevelException("AMQP does not support an isolation level concept");
            }

            var txObject = (RabbitTransactionObject)transaction;
            RabbitResourceHolder resourceHolder = null;
            try
            {
                resourceHolder = ConnectionFactoryUtils.GetTransactionalResourceHolder(this.ConnectionFactory, true);
                if (this.logger.IsDebugEnabled)
                {
                    this.logger.Debug("Created AMQP transaction on channel [" + resourceHolder.Channel + "]");
                }

                // resourceHolder.DeclareTransactional();
                txObject.ResourceHolder = resourceHolder;
                txObject.ResourceHolder.SynchronizedWithTransaction = true;
                var timeout = DetermineTimeout(definition);
                if (timeout != DefaultTransactionDefinition.TIMEOUT_DEFAULT)
                {
                    txObject.ResourceHolder.TimeoutInSeconds = timeout;
                }

                TransactionSynchronizationManager.BindResource(this.ConnectionFactory, txObject.ResourceHolder);
            }
            catch (AmqpException ex)
            {
                if (resourceHolder != null)
                {
                    ConnectionFactoryUtils.ReleaseResources(resourceHolder);
                }

                throw new CannotCreateTransactionException("Could not create AMQP transaction", ex);
            }
        }

        /// <summary>
        /// Do suspend.
        /// </summary>
        /// <param name="transaction">
        /// The transaction.
        /// </param>
        /// <returns>
        /// The object.
        /// </returns>
        protected override object DoSuspend(object transaction)
        {
            var txObject = (RabbitTransactionObject)transaction;
            txObject.ResourceHolder = null;
            return TransactionSynchronizationManager.UnbindResource(this.ConnectionFactory);
        }

        /// <summary>
        /// Do resume.
        /// </summary>
        /// <param name="transaction">
        /// The transaction.
        /// </param>
        /// <param name="suspendedResources">
        /// The suspended resources.
        /// </param>
        protected override void DoResume(object transaction, object suspendedResources)
        {
            var conHolder = (RabbitResourceHolder)suspendedResources;
            TransactionSynchronizationManager.BindResource(this.ConnectionFactory, conHolder);
        }

        /// <summary>
        /// Do commit.
        /// </summary>
        /// <param name="status">
        /// The status.
        /// </param>
        protected override void DoCommit(DefaultTransactionStatus status)
        {
            var txObject = (RabbitTransactionObject)status.Transaction;
            var resourceHolder = txObject.ResourceHolder;
            resourceHolder.CommitAll();
        }

        /// <summary>
        /// Do rollback.
        /// </summary>
        /// <param name="status">
        /// The status.
        /// </param>
        protected override void DoRollback(DefaultTransactionStatus status)
        {
            var txObject = (RabbitTransactionObject)status.Transaction;
            var resourceHolder = txObject.ResourceHolder;
            resourceHolder.RollbackAll();
        }

        /// <summary>
        /// Do set rollback only.
        /// </summary>
        /// <param name="status">
        /// The status.
        /// </param>
        protected void DoSetRollbackOnly(DefaultTransactionStatus status)
        {
            var txObject = (RabbitTransactionObject)status.Transaction;
            txObject.ResourceHolder.RollbackOnly = true;
        }

        /// <summary>
        /// Do cleanup after completion.
        /// </summary>
        /// <param name="transaction">
        /// The transaction.
        /// </param>
        protected void DoCleanupAfterCompletion(object transaction)
        {
            var txObject = (RabbitTransactionObject)transaction;
            TransactionSynchronizationManager.UnbindResource(this.ConnectionFactory);
            txObject.ResourceHolder.CloseAll();
            txObject.ResourceHolder.Clear();
        }
    }

    /// <summary>
    /// A rabbit transaction object, representing a RabbitResourceHolder. Used as transaction object by RabbitTransactionManager.
    /// </summary>
    internal class RabbitTransactionObject : ISmartTransactionObject
    {
        /// <summary>
        /// The resource holder.
        /// </summary>
        private RabbitResourceHolder resourceHolder;

        /// <summary>
        /// Gets or sets ResourceHolder.
        /// </summary>
        public RabbitResourceHolder ResourceHolder
        {
            get { return this.resourceHolder; }
            set { this.resourceHolder = value; }
        }

        /// <summary>
        /// Gets a value indicating whether RollbackOnly.
        /// </summary>
        public bool RollbackOnly
        {
            get { return this.resourceHolder.RollbackOnly; }
        }

        /// <summary>
        /// Flush the object.
        /// </summary>
        public void Flush()
        {
            // no-op
        }
    }
}
