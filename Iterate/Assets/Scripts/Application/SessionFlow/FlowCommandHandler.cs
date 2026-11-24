using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Progression;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Applies the commands that move the Session itself: execution, the Result Review exits, route
    /// choice and abandonment. An execution runs in a fixed order: compile, build the request, pay the
    /// cost, execute, apply. The request is built before the cost is paid, so a content defect cannot
    /// throw after Bytes are spent. The final execution resolves itself: at Pass or above it is
    /// submitted, below Pass the Process fails.
    /// </summary>
    internal sealed class FlowCommandHandler
    {
        private const ChangedAreas ExecutionAreas = ChangedAreas.Bytes | ChangedAreas.Counters | ChangedAreas.Execution;
        private const ChangedAreas SubmitAreas = ChangedAreas.Execution | ChangedAreas.Rewards | ChangedAreas.Economy | ChangedAreas.Repository;
        private readonly StageTransitions _transitions;

        /// <summary>
        /// Creates the handler over the stage transitions.
        /// </summary>
        /// <param name="transitions">The transitions.</param>
        internal FlowCommandHandler(StageTransitions transitions)
        {
            _transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        /// <summary>
        /// Compiles, pays, executes and applies one execution, then enters Result Review, or resolves
        /// the final execution.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The command.</param>
        /// <returns>The execution outcome, the refused counter, or the uncommitted attempt.</returns>
        internal HandlerOutcome CompileAndRun(SessionContext context, ISessionCommand command)
        {
            ProcessState process = context.Process;
            if (!process.Counters.CanExecute)
                return new HandlerOutcome(false, process.Counters.RecordExecution(), ChangedAreas.None);

            CompilationAttempt attempt = context.Build.Compile(process.Bytes.Balance, process.CompilationEffects);
            if (!attempt.Committed)
                return new HandlerOutcome(false, attempt, ChangedAreas.None);

            ExecutionRequest request = ExecutionRequestFactory.Build(context, attempt.Source);
            process.Bytes.Debit(new ByteAmount(attempt.Breakdown.FinalCost), "compilation");
            ExecutionRecord record = new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
            ThresholdEvaluation evaluation = ExecutionApplication.Apply(context, record);
            ExecutionOutcome outcome = new(attempt, record, evaluation);

            if (process.Counters.CanExecute)
            {
                context.Stage = SessionStage.ResultReview;
                return new HandlerOutcome(true, outcome, ExecutionAreas);
            }

            if (evaluation.Reached.HasValue)
                return new HandlerOutcome(true, outcome, ExecutionAreas | Submit(context).Areas);

            return new HandlerOutcome(true, outcome, ExecutionAreas | _transitions.Fail(context));
        }

        /// <summary>
        /// Submits the reviewed result and enters the rewards stage.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The command.</param>
        /// <returns>The submit result.</returns>
        internal HandlerOutcome SubmitReviewed(SessionContext context, ISessionCommand command)
        {
            CurrentResult result = context.Result;
            if (result == null || result.Discarded)
                return Refused(SubmitRejection.NoCurrentResult);

            if (!result.Evaluation.Reached.HasValue)
                return Refused(SubmitRejection.BelowPass);

            if (context.Process.Counters.MandatoryExecutionsRemaining > 0)
                return Refused(SubmitRejection.MandatoryExecutionsRemain);

            return Submit(context);
        }

        /// <summary>
        /// Discards a result at Pass or above and returns to Build for another execution.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The command.</param>
        /// <returns>The arrival result, or the evaluation that refuses the discard.</returns>
        internal HandlerOutcome Discard(SessionContext context, ISessionCommand command)
        {
            CurrentResult result = context.Result;
            if (!result.Evaluation.Reached.HasValue)
                return new HandlerOutcome(false, result.Evaluation, ChangedAreas.None);

            context.Result = result with { Discarded = true };
            return ReturnToBuild(context);
        }

        /// <summary>
        /// Leaves a below-Pass result for Build without confirmation.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The command.</param>
        /// <returns>The arrival result, or the evaluation that refuses the exit.</returns>
        internal HandlerOutcome LeaveReview(SessionContext context, ISessionCommand command)
        {
            CurrentResult result = context.Result;
            if (result.Evaluation.Reached.HasValue)
                return new HandlerOutcome(false, result.Evaluation, ChangedAreas.None);

            context.Result = null;
            return ReturnToBuild(context);
        }

        /// <summary>
        /// Commits to a route and enters the route's shop.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The <see cref="SelectRoute"/> command.</param>
        /// <returns>An accepted outcome, or a refusal when the route is not offered.</returns>
        internal HandlerOutcome ChooseRoute(SessionContext context, ISessionCommand command)
        {
            SelectRoute select = (SelectRoute)command;
            if (!context.Plan.TryCommitRoute(select.Route))
                return new HandlerOutcome(false, null, ChangedAreas.None);

            return new HandlerOutcome(true, null, _transitions.AdvanceToNextStep(context));
        }

        /// <summary>
        /// Abandons the Session from any active stage.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="command">The command.</param>
        /// <returns>An accepted outcome; abandonment cannot be refused.</returns>
        internal HandlerOutcome Abandon(SessionContext context, ISessionCommand command)
        {
            _transitions.Abandon(context);
            return new HandlerOutcome(true, null, ChangedAreas.None);
        }

        /// <summary>
        /// Submits the current result: plans and applies the Process's reward package at the tier
        /// reached, enters the rewards stage, and leaves it at once when nothing is pending.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The accepted submit outcome.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the reward package is missing or does not plan.</exception>
        private HandlerOutcome Submit(SessionContext context)
        {
            ContentCatalog catalog = context.Catalog;
            ProcessConfigurationDefinition configuration = context.Process.Configuration;
            if (!catalog.TryGetRewardPackage(configuration.RewardPackage, out RewardPackageDefinition package))
                throw new InvalidOperationException($"The catalog does not define {configuration.RewardPackage.Value}, which {configuration.ID.Value} names.");

            RewardPlanResult planned = RewardResolver.Plan(package, context.Result.Evaluation.Reached, context.Session, catalog);
            if (!planned.Succeeded)
                throw new InvalidOperationException($"The reward package {package.ID.Value} did not plan: {planned.Rejection}.");

            RewardResolution resolution = RewardResolver.Apply(planned.Plan, context.Session, catalog);
            context.Rewards = resolution;
            context.Stage = SessionStage.Rewards;
            ChangedAreas finished = _transitions.FinishRewardsIfResolved(context);
            return new HandlerOutcome(true, new SubmitResult(true, SubmitRejection.None, resolution), SubmitAreas | finished);
        }

        /// <summary>
        /// Fires the arrival moment after the last execution and returns to Build.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The accepted outcome carrying the arrival result.</returns>
        private static HandlerOutcome ReturnToBuild(SessionContext context)
        {
            ProcessState process = context.Process;
            ArrivalResult arrived = process.Arrive(new ArrivalMoment(process.Counters.ExecutionsRun));
            context.Stage = SessionStage.Build;
            return new HandlerOutcome(true, arrived, ChangedAreas.Buffer | ChangedAreas.Execution);
        }

        /// <summary>
        /// A refused submission.
        /// </summary>
        /// <param name="rejection">Why it was refused.</param>
        /// <returns>The refused outcome.</returns>
        private static HandlerOutcome Refused(SubmitRejection rejection)
        {
            return new HandlerOutcome(false, new SubmitResult(false, rejection, null), ChangedAreas.None);
        }
    }
}