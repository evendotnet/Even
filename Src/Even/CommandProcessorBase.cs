using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public abstract class CommandProcessorBase : ReceiveActor
    {
        Dictionary<Type, Func<object, Task>> _commandProcessors = new Dictionary<Type, Func<object, Task>>();
        internal CommandContext CurrentCommand { get; private set; }

        ILoggingAdapter _log = Context.GetLogger();

        /// <summary>
        /// A reference to the supervisor that created the processor.
        /// </summary>
        protected abstract IActorRef ProcessorSupervisor { get; }

        /// <summary>
        /// The amount of time the processor will sit idle before shutting down
        /// </summary>
        protected abstract TimeSpan? IdleTimeout { get; }

        /// <summary>
        /// Setup the actor to receive command requests and the idle timeout.
        /// </summary>
        internal void SetupBase()
        {
            Receive<CommandRequest>(request =>
            {
                if (request.Retries >= 3)
                {
                    FailCommand(new Exception("Too many retries"));
                    return;
                }

                _log.Debug("{0}: Command Received", request.CommandID);

                // locate the command processor
                Func<object, Task> processor;

                if (!_commandProcessors.TryGetValue(request.Command.GetType(), out processor))
                {
                    RefuseCommand(Sender, request, "Command not supported");
                    return;
                }

                _log.Debug("{0}: Command Accepted", request.CommandID);

                // create the context process the command

                CurrentCommand = new CommandContext { Request = request, Sender = Sender };

                OnBeforeProcessCommand();

                AkkaAsyncHelper.Await(async () => {

                    try
                    {
                        await processor(request.Command);
                    }
                    catch (Exception ex)
                    {
                        FailCommand(ex);

                        CurrentCommand = null;

                        OnCommandFail();
                        return;
                    }

                    // if the subclass won't handle any persistence request,
                    // we just return the command as successful

                    var willWait = HandlePersistenceRequest();

                    if (!willWait)
                    {
                        CurrentCommand = null;
                        AcceptCommand();
                        OnCommandSucceeded();
                    }
                });
            });

            // setup the idle timeout if needed

            var idleTimeout = IdleTimeout;

            if (idleTimeout != null)
            {
                SetReceiveTimeout(idleTimeout.Value);

                Receive<ReceiveTimeout>(_ =>
                {
                    // in order to ensure all commands are delivered to command processors
                    // without dead letters, these actors don't stop suddenly. instead, they
                    // notify the supervisor that they will stop and wait for a while to give
                    // time for the supervisor to remove them from the list
                    // if no new messages arrive for some time than it stops
                    ProcessorSupervisor.Tell(new WillStop());

                    Become(AwaitingToStop);
                });
            }
        }

        void AwaitingToStop()
        {
            SetReceiveTimeout(TimeSpan.FromSeconds(2));

            // at this point, it's not safe to do any work,
            // so we forward any new requests to the supervisor so it can forward 
            // back to the right instance
            Receive<CommandRequest>(request => ProcessorSupervisor.Forward(request));

            Receive<ReceiveTimeout>(_ =>
            {
                _log.Debug("Stopping idle command processor");
                Context.Stop(Self);
            });
        }

        protected void OnBeforeProcessCommand()
        { }

        protected void OnCommandFail()
        { }

        protected void OnCommandSucceeded()
        { }

        /// <summary>
        /// Handles any persistence requests and returns true if it
        /// will need to wait for it, or false if we can accept the command.
        /// </summary>
        internal abstract bool HandlePersistenceRequest();

        internal void AcceptCommand()
        {
            _log.Debug("{0}: Command Accepted", CurrentCommand.Request.CommandID);
            CurrentCommand.Sender.Tell(new CommandSucceeded { CommandID = CurrentCommand.Request.CommandID });
        }

        internal void FailCommand(Exception ex)
        {
            _log.Debug("{0}: Command Failed - {1}", CurrentCommand.Request.CommandID, ex.Message);
            CurrentCommand.Sender.Tell(new CommandFailed { CommandID = CurrentCommand.Request.CommandID, Exception = ex });
        }

        internal void RefuseCommand(IActorRef sender, CommandRequest cmd, string message)
        {
            _log.Debug("{0}: Command Refused with {1}", cmd.CommandID, message);
            Sender.Tell(new CommandRefused { CommandID = cmd.CommandID, Reason = "Invalid Stream" });
        }

        #region Command Handler Registration

        protected void OnCommand<T>(Action<T> action)
        {
            _commandProcessors.Add(typeof(T), o => {
                action((T)o);
                return Task.CompletedTask;
            });
        }

        protected void OnCommand<T>(Func<T, Task> func)
        {
            _commandProcessors.Add(typeof(T), o => func((T)o));
        }

        #endregion

        #region Command Handler Actions

        /// <summary>
        /// Causes the command to interrupt processing imediatelly and reply to the sender.
        /// </summary>
        protected virtual void Fail(string message)
        {
            throw new Exception(message);
        }

        #endregion
    }

    internal class CommandContext
    {
        public IActorRef Sender { get; set; }
        public CommandRequest Request { get; set; }
    }
}
