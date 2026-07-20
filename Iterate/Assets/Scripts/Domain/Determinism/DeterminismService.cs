using System;
using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The stateless Determinism Service. <see cref="Decide"/> is a pure function returning the outcome,
    /// the complete record, and the ordered event payloads; the per-decision PCG stream is created inside
    /// Decide and never escapes. A population too small for the method routes through the declared
    /// insufficiency behavior — never an exception; only authoring/data defects throw.
    /// </summary>
    public sealed class DeterminismService
    {
        /// <summary>
        /// The published random-service revision identity stamped on every record.
        /// </summary>
        public const string RevisionIdentity = "iterate-rng-1";

        /// <summary>
        /// The fixed PCG stream selector for a decision's stream, part of the revision compatibility surface.
        /// </summary>
        private const ulong DecisionStreamSequence = 54UL;

        /// <summary>
        /// Resolves a random decision over the captured snapshot.
        /// </summary>
        /// <param name="request">The complete decision declaration.</param>
        /// <param name="snapshot">The captured candidate snapshot in canonical order.</param>
        /// <returns>The outcome, complete record, and ordered event payloads.</returns>
        public DecisionResult Decide(DecisionRequest request, CandidateSnapshot snapshot)
        {
            if (request == null)
                throw new ArgumentException("A decision requires a request.", nameof(request));
            
            if (snapshot == null)
                throw new ArgumentException("A decision requires a candidate snapshot.", nameof(snapshot));

            bool weightedMethod = request.Method == SelectionMethod.WeightedSelection;
            if (weightedMethod != snapshot.IsWeighted)
            {
                throw new ArgumentException(
                    "A weighted method requires a weighted snapshot, and a non-weighted method an unweighted one.",
                    nameof(snapshot)
                );
            }

            List<DrawRecord> draws = new List<DrawRecord>();
            List<string> resultPermutation = new List<string>();
            Pcg32 stream = new Pcg32(request.Context.Hash, DecisionStreamSequence);

            bool isShuffle = request.Method == SelectionMethod.DeterministicShuffle;
            DecisionOutcome outcome;
            RandomSelectionEventKind terminalKind;

            bool insufficient = !isShuffle
                                && (snapshot.Count < RequiredMinimumPopulation(request) 
                                || (weightedMethod && TotalWeight(snapshot) == 0UL));
            
            if (insufficient)
            {
                switch (request.InsufficientCandidates)
                {
                    case InsufficientCandidateBehavior.FailToQualify:
                        outcome = new DecisionOutcome(DecisionDisposition.FailedToQualify, new List<string>());
                        terminalKind = RandomSelectionEventKind.SelectionFailed;
                        break;
                    
                    case InsufficientCandidateBehavior.CancelTheDecision:
                        outcome = new DecisionOutcome(DecisionDisposition.Cancelled, new List<string>());
                        terminalKind = RandomSelectionEventKind.SelectionFailed;
                        break;
                    
                    case InsufficientCandidateBehavior.UseDeclaredFallback:
                        outcome = new DecisionOutcome(DecisionDisposition.FallbackResolved, new List<string>());
                        terminalKind = RandomSelectionEventKind.FallbackResolved;
                        break;
                    
                    case InsufficientCandidateBehavior.SelectAllRemainingCandidates:
                        outcome = SelectAllRemaining(request, snapshot, draws);
                        terminalKind = RandomSelectionEventKind.SelectionCompleted;
                        break;
                    
                    case InsufficientCandidateBehavior.ReduceSelectionCount:
                        DecisionOutcome reduced = StrategyFor(request.Method).Select(request, snapshot, stream, snapshot.Count, draws, resultPermutation);
                        outcome = new DecisionOutcome(DecisionDisposition.ReducedCount, reduced.SelectedIdentities);
                        terminalKind = RandomSelectionEventKind.SelectionCompleted;
                        break;
                    
                    default:
                        throw new ArgumentException(
                            "Unrecognized insufficient-candidate behavior.",
                            nameof(request)
                        );
                }
            }
            else
            {
                int effectiveCount = isShuffle ? snapshot.Count : request.SelectionCount;
                outcome = StrategyFor(request.Method).Select(request, snapshot, stream, effectiveCount, draws, resultPermutation);
                terminalKind = RandomSelectionEventKind.SelectionCompleted;
            }

            List<RandomSelectionEvent> events = BuildEvents(request.SelectionIdentity, draws, terminalKind);
            RandomDecisionRecord record = new RandomDecisionRecord(
                request,
                snapshot.Candidates,
                RevisionIdentity,
                draws,
                resultPermutation,
                outcome
            );
            return new DecisionResult(outcome, record, events);
        }

        /// <summary>
        /// The minimum population the method needs before it is insufficient: the selection count for
        /// methods that consume candidates, or one for methods that reuse or draw a single candidate
        /// (with replacement can exceed the population, CAB-EVT-816).
        /// </summary>
        /// <param name="request">The decision request.</param>
        /// <returns>The minimum sufficient population size.</returns>
        private static int RequiredMinimumPopulation(DecisionRequest request)
        {
            switch (request.Method)
            {
                case SelectionMethod.UniformSelectionWithReplacement:
                case SelectionMethod.UniformSingleSelection:
                    return 1;
                
                case SelectionMethod.WeightedSelection:
                    return request.Replacement == ReplacementBehavior.RemainsEligible ? 1 : request.SelectionCount;
                
                default:
                    return request.SelectionCount;
            }
        }

        /// <summary>
        /// Selects every remaining candidate in canonical order, recording one draw per candidate.
        /// </summary>
        /// <param name="request">The decision request.</param>
        /// <param name="snapshot">The candidate snapshot.</param>
        /// <param name="draws">The buffer to append draw records to.</param>
        /// <returns>The select-all-remaining outcome.</returns>
        private static DecisionOutcome SelectAllRemaining(
            DecisionRequest request,
            CandidateSnapshot snapshot,
            List<DrawRecord> draws
        )
        {
            List<string> working = SelectionSupport.Identities(snapshot.Candidates);
            List<string> selectedIdentities = new List<string>(working.Count);
            int total = working.Count;
            for (int drawIndex = 0; drawIndex < total; drawIndex++)
            {
                List<string> before = new List<string>(working);
                string selected = working[0];
                working.RemoveAt(0);
                List<string> after = new List<string>(working);
                draws.Add(new DrawRecord(drawIndex + 1, before, selected, request.Replacement, after));
                selectedIdentities.Add(selected);
            }

            return new DecisionOutcome(DecisionDisposition.SelectedAllRemaining, selectedIdentities);
        }

        /// <summary>
        /// Resolves the sealed strategy for a selection method.
        /// </summary>
        /// <param name="method">The selection method.</param>
        /// <returns>The strategy implementing the method.</returns>
        private static ISelectionStrategy StrategyFor(SelectionMethod method)
        {
            switch (method)
            {
                case SelectionMethod.UniformSingleSelection:
                    return new UniformSingleStrategy();
                
                case SelectionMethod.UniformSelectionWithoutReplacement:
                    return new UniformSelectionWithoutReplacementStrategy();
                
                case SelectionMethod.UniformSelectionWithReplacement:
                    return new UniformSelectionWithReplacementStrategy();
                
                case SelectionMethod.WeightedSelection:
                    return new WeightedSelectionStrategy();
                
                case SelectionMethod.DeterministicShuffle:
                    return new DeterministicShuffleStrategy();
                
                case SelectionMethod.RandomOrderingOfCapturedFiniteSet:
                    return new RandomOrderingStrategy();
                
                default:
                    throw new ArgumentException("The selection method is not supported.", nameof(method));
            }
        }

        /// <summary>
        /// Builds the ordered event payloads: creation, snapshot capture, one draw-resolved per draw, then
        /// the terminal event.
        /// </summary>
        /// <param name="selectionIdentity">The selection identity carried by every event.</param>
        /// <param name="draws">The resolved draws.</param>
        /// <param name="terminalKind">The terminal event kind.</param>
        /// <returns>The ordered event list.</returns>
        private static List<RandomSelectionEvent> BuildEvents(
            string selectionIdentity,
            List<DrawRecord> draws,
            RandomSelectionEventKind terminalKind
        )
        {
            List<RandomSelectionEvent> events = new List<RandomSelectionEvent>();
            events.Add(new RandomSelectionEvent(RandomSelectionEventKind.DecisionCreated, selectionIdentity, 0));
            events.Add(new RandomSelectionEvent(RandomSelectionEventKind.CandidateSnapshotCaptured, selectionIdentity, 0));
            for (int index = 0; index < draws.Count; index++)
            {
                events.Add(new RandomSelectionEvent(
                    RandomSelectionEventKind.DrawResolved,
                    selectionIdentity,
                    draws[index].DrawOrdinal)
                );
            }

            events.Add(new RandomSelectionEvent(terminalKind, selectionIdentity, 0));
            return events;
        }

        /// <summary>
        /// Calculates the total weight of all candidates in a given candidate snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot containing the candidates and their associated weights.</param>
        /// <returns>The sum of weights for all candidates in the snapshot.</returns>
        private static ulong TotalWeight(CandidateSnapshot snapshot)
        {
            ulong total = 0UL;
            for (int index = 0; index < snapshot.Count; index++)
            {
                CandidateEntry entry = snapshot.Candidates[index];
                if (entry.FinalWeight.HasValue)
                    total += (ulong)entry.FinalWeight.Value;
            }

            return total;
        }
    }
}