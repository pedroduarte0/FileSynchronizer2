Feature: Sync readiness
  The scaffold should expose the first public sync seam to integration tests.

  Scenario: One-way sync mode is available
    Given the sync readiness provider is available
    When the integration test asks which sync modes are supported
    Then one-way sync should be supported
