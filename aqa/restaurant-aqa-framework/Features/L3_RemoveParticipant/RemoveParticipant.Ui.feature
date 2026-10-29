@L3 @ui
Feature: L3 UI. Owner removes a guest in the app

  Background:
    Given the owner has booked a table for 5 guests
    And the owner has shared the group invite

  @smoke
  Scenario: Owner removes one of several guests
    Given these guests have joined the group by the invite:
      | guest | dishes            | cart      |
      | Anna  | 2 x Garden Soup   | submitted |
      | Boris | 1 x Veggie Burger | submitted |
      | Clara | 1 x Green Salad   | open      |
    And the owner has opened the group pre-order page
    And the grand total shown is "$29.00"
    When the owner removes guest Boris in the app
    Then the app confirms "Boris was removed from the reservation"
    And the card "Guest: Boris" is not shown
    And the owner sees the cards of "Anna, Clara"
    And the consolidated pre-order shows:
      | dish        | quantity |
      | Garden Soup | 2        |
    And the grand total shown is "$17.00"

  Scenario: Owner removes the last guest
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
    And the owner has opened the group pre-order page
    When the owner removes guest Anna in the app
    Then the owner sees only their own card "My Pre-Order"
    And the page says "Participants haven't added any dishes yet"
    And the grand total shown is "$0.00"

  Scenario: Owner changes their mind in the confirmation dialog
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
    And the owner has opened the group pre-order page
    When the owner starts removing guest Anna but cancels
    Then the owner sees the cards of "Anna"
    And the grand total shown is "$8.50"
    And guest Anna is still in the group

  Scenario: The owner's own card cannot be removed
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart |
      | Anna  | 1 x Garden Soup | open |
    When the owner opens the group pre-order page
    Then there is no "Remove Guest" button for the owner's own card
