Feature: One-way sync preview
  The scaffold should expose the first public sync application service seam to integration tests.

  Scenario: Empty one-way sync pair previews no file actions
    Given an empty one-way sync pair
    When the sync preview is requested
    Then the sync plan should contain no file actions
