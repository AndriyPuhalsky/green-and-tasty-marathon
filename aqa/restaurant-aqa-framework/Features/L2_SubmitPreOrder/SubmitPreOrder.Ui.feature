@L2 @ui
Feature: L2 UI. Owner submits the group pre-order in the app and the waiter gets it

  Background:
    Given the owner has booked a table for 5 guests
    And the owner has shared the group invite

  @smoke
  Scenario: Owner sends the consolidated pre-order to the restaurant
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes                           | cart      |
      | Anna  | 2 x Garden Soup                  | submitted |
      | Boris | 1 x Garden Soup, 1 x Green Salad | submitted |
      | Clara | 1 x Veggie Burger                | open      |
    And the owner has opened the group pre-order page
    Then the consolidated pre-order shows:
      | dish        | quantity |
      | Garden Soup | 3        |
      | Green Salad | 1        |
    And the grand total shown is "$31.75"
    When the owner opens the submit dialog
    Then the dialog says "The restaurant will get 4 dishes for $31.75"
    When the owner confirms the submit
    Then the app confirms "Pre-order sent to the restaurant"
    And the pre-order status shown is "Sent to Restaurant"
    And the "Pre-Order Sent" button is disabled
    And no "Remove Guest" buttons are shown
    And the restaurant order has status "SubmittedByCustomer" with dishes:
      | dish        | quantity |
      | Berry Cake  | 1        |
      | Garden Soup | 3        |
      | Green Salad | 1        |

  @smoke @waiter
  Scenario: Waiter sees the pre-order the owner has submitted
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes                           | cart      |
      | Anna  | 2 x Garden Soup                  | submitted |
      | Boris | 1 x Garden Soup, 1 x Green Salad | submitted |
      | Clara | 1 x Veggie Burger                | open      |
    And the owner has submitted the group pre-order in the app
    When the waiter opens the pre-order of the owner's reservation
    Then the waiter sees these dishes:
      | dish        | quantity |
      | Berry Cake  | 1        |
      | Garden Soup | 3        |
      | Green Salad | 1        |

  Scenario: Submit is not available while nobody has submitted a cart
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart |
      | Anna  | 1 x Garden Soup | open |
    When the owner opens the group pre-order page
    Then the consolidated pre-order says "Nobody has submitted a cart yet."
    And the "Submit Pre-Order" button is disabled

  Scenario: Owner sees why the submit failed when the reservation has no pre-order
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
    And the owner has opened the group pre-order page
    When the owner submits the group pre-order in the app
    Then the app shows the error "Pre-order for reservation '{reservation id}' was not found."
    And the pre-order status shown is "Collecting pre-orders"
    And the group reservation has status "Open"

  @known-defect
  Scenario: Submitted group stays visible and read-only after the page is reopened
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 2 x Garden Soup | submitted |
    And the owner has submitted the group pre-order
    When the owner opens the group pre-order page
    Then the pre-order status shown is "Sent to Restaurant"
    And the "Pre-Order Sent" button is disabled
    And no "Remove Guest" buttons are shown
