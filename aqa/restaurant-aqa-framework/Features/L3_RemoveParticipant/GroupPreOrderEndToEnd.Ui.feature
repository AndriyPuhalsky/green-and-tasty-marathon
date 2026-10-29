@L3 @ui @e2e
Feature: L3 end to end. From the guests' carts to the waiter's screen

  @smoke @waiter
  Scenario: Removed guest does not get into the order the waiter receives
    Given the owner has booked a table for 5 guests
    And the owner has shared the group invite
    And the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes                            | cart      |
      | Anna  | 2 x Garden Soup                   | submitted |
      | Boris | 1 x Veggie Burger, 1 x Green Salad | submitted |
      | Clara | 1 x Garden Soup                   | submitted |
    And the owner has opened the group pre-order page
    Then the owner sees these participant cards:
      | card         | status         | dishes                             | subtotal |
      | My Pre-Order | Still choosing |                                    | $0.00    |
      | Guest: Anna  | Submitted      | 2 x Garden Soup                    | $17.00   |
      | Guest: Boris | Submitted      | 1 x Veggie Burger, 1 x Green Salad | $18.25   |
      | Guest: Clara | Submitted      | 1 x Garden Soup                    | $8.50    |
    And the grand total shown is "$43.75"
    When the owner removes guest Boris in the app
    Then the grand total shown is "$25.50"
    When the owner submits the group pre-order in the app
    Then the app confirms "Pre-order sent to the restaurant"
    And the pre-order status shown is "Sent to Restaurant"
    When the waiter opens the pre-order of the owner's reservation
    Then the waiter sees these dishes:
      | dish        | quantity |
      | Berry Cake  | 1        |
      | Garden Soup | 3        |
