@L1 @ui
Feature: L1 UI. Owner sees the group pre-order in the app
  Guests and their carts are prepared through the API, everything the owner
  does and sees goes through the web app.

  Background:
    Given the owner has booked a table for 5 guests
    And the owner has shared the group invite

  @smoke
  Scenario: Owner opens the group pre-order from the reservations page
    Given these guests have joined the group by the invite:
      | guest | dishes                           | cart      |
      | Anna  | 2 x Garden Soup, 1 x Green Salad | submitted |
      | Boris | 1 x Veggie Burger                | open      |
    And the owner is signed in to the app
    When the owner opens the group pre-order from the reservations page
    Then the owner sees these participant cards:
      | card         | status         | dishes                           | subtotal |
      | My Pre-Order | Still choosing |                                  | $0.00    |
      | Guest: Anna  | Submitted      | 2 x Garden Soup, 1 x Green Salad | $23.25   |
      | Guest: Boris | Still choosing | 1 x Veggie Burger                | $12.00   |
    And the consolidated pre-order shows:
      | dish        | quantity |
      | Garden Soup | 2        |
      | Green Salad | 1        |
    And the grand total shown is "$23.25"
    And the page warns that some participants haven't submitted their cart yet

  Scenario: Grand total changes when a guest updates and submits the cart
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
      | Boris | 2 x Berry Cake  | open      |
    And the owner has opened the group pre-order page
    And the grand total shown is "$8.50"
    When guest Boris adds "1 x Green Salad" to the cart
    And guest Boris submits the cart
    And the owner refreshes the participants
    Then the card "Guest: Boris" shows status "Submitted" and subtotal "$17.75"
    And the grand total shown is "$26.25"

  Scenario: Group where nobody has joined yet
    When the owner opens the group pre-order page
    Then the owner sees only their own card "My Pre-Order"
    And the page says "Participants haven't added any dishes yet"
    And the grand total shown is "$0.00"
    And the "Submit Pre-Order" button is disabled

  @known-defect
  Scenario: Owner opens the page when every seat in the group is taken
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
      | Boris |                 | open      |
      | Clara |                 | open      |
      | Dan   |                 | open      |
    When the owner opens the group pre-order page
    Then the owner sees the cards of "Anna, Boris, Clara, Dan"
    And the grand total shown is "$8.50"

  Scenario: Group pre-order of a reservation that does not exist
    Given the owner is signed in to the app
    When the owner opens the group pre-order of a reservation that does not exist
    Then the page says "Group reservation not found"

  Scenario: Another customer cannot see the owner's group
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
    And another customer is signed in to the app
    When another customer opens the owner's group pre-order by link
    Then another customer sees the message "Couldn't load the group pre-order" and no participant cards
