Feature: One-way local mirror planning
  One-way sync previews the changes needed to mirror a local source location to a local target location.

  Scenario: New source file is planned as a copy
    Given a local one-way sync pair
    And the source location contains "notes.txt" with content "new"
    When the sync preview is requested
    Then the sync plan should contain a copy action for "notes.txt"

  Scenario: Changed source file is planned as an overwrite
    Given a local one-way sync pair
    And the source location contains "notes.txt" with content "updated"
    And the target location contains "notes.txt" with content "old"
    When the sync preview is requested
    Then the sync plan should contain an overwrite action for "notes.txt"

  Scenario: Target extra is planned as a deletion
    Given a local one-way sync pair
    And the target location contains "stale.txt" with content "old"
    When the sync preview is requested
    Then the sync plan should contain a delete action for "stale.txt"

  Scenario: Overlapping local locations are blocked
    Given overlapping local sync locations
    When the sync preview is requested
    Then the sync plan should report an overlapping location
