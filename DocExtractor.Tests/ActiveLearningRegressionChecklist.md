# Active Learning Regression Checklist

This checklist is used after any NLP active-learning change.

## Automated tests

- Run `ActiveLearningWorkflowTests.Queue_ShouldDeduplicateAndTrackSkipState`
- Run `ActiveLearningWorkflowTests.EndToEnd_ShouldHandleSentenceParagraphAndArticleInputs`

## Manual UI regression

1. Open `NLP 主动学习实验室` from main window.
2. Create/select a scenario and import a text file containing:
   - one sentence
   - one paragraph
   - one multi-paragraph article
3. Verify uncertain queue is populated and duplicate rows are not re-added.
4. Skip one item and confirm pending queue count decreases immediately.
5. Annotate at least 20 verified samples and run incremental training.
6. Confirm quality gate behavior:
   - if `F1_test < 0.95`, model is rolled back
   - if `F1_test >= 0.95` and improved, model is published
7. Open dashboard and verify:
   - trend chart updates
   - per-type metrics populate
   - cross-scenario matrix shows weakest scenario note

## Acceptance gate

Only accept release when:

- sentence/paragraph/article extraction flow is stable
- active learning loop is complete (predict -> correct -> train -> gate -> dashboard)
- target scenario keeps `F1 >= 0.95` on held-out test set
