@L2 @api
Feature: L2 API. Owner submits the consolidated group pre-order
  Only submitted carts go to the restaurant, the same dishes are merged,
  the order becomes SubmittedByCustomer and the group becomes Submitted.
  The owner's own dishes come from the personal pre-order of the reservation,
  without it the backend has no order to put the group dishes into.

  Background:
    Given the owner has booked a table for 5 guests
    And the owner has shared the group invite

  @smoke
  Scenario: Submitted carts are merged into the restaurant order
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes                           | cart      |
      | Anna  | 2 x Garden Soup, 1 x Green Salad | submitted |
      | Boris | 1 x Garden Soup                  | submitted |
      | Clara | 1 x Veggie Burger                | open      |
    When the owner submits the group pre-order
    Then the request succeeds
    And the submitted dishes are:
      | dish        | quantity |
      | Garden Soup | 3        |
      | Green Salad | 1        |
    And the restaurant order has status "SubmittedByCustomer" with dishes:
      | dish        | quantity |
      | Berry Cake  | 1        |
      | Garden Soup | 3        |
      | Green Salad | 1        |
    And the group reservation has status "Submitted"

  Scenario: The same dish already in the personal pre-order is summed up
    Given the owner has a personal pre-order with "1 x Garden Soup"
    And these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 2 x Garden Soup | submitted |
    When the owner submits the group pre-order
    Then the request succeeds
    And the restaurant order has status "SubmittedByCustomer" with dishes:
      | dish        | quantity |
      | Garden Soup | 3        |

  @known-defect
  Scenario: Owner adds dishes to their own session in the group
    Given the owner has opened their own session in the group
    When the owner adds "1 x Veggie Burger" to their own session
    Then the request succeeds
    And the participants are:
      | name      | type  | dishes            | total | submitted |
      | the owner | Owner | 1 x Veggie Burger | 12.00 | no        |

  Scenario: Submit is rejected when no guest has submitted a cart
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes          | cart |
      | Anna  | 1 x Garden Soup | open |
    When the owner submits the group pre-order
    Then the request is rejected with status 400 and message "None of the guest sessions has been submitted yet."
    And the group reservation has status "Open"

  Scenario: An empty cart cannot be submitted, so there is nothing to send
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And guest Anna has joined the group by the invite
    When guest Anna submits the cart
    Then the request is rejected with status 400 and message "The cart is empty"
    When the owner submits the group pre-order
    Then the request is rejected with status 400 and message "None of the guest sessions has been submitted yet."

  Scenario: Submit is rejected when the reservation has no pre-order
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
    When the owner submits the group pre-order
    Then the request is rejected with status 400 and message "Pre-order for reservation '{reservation id}' was not found."
    And the group reservation has status "Open"

  Scenario Outline: Submit is rejected when <what> does not exist
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
    When the owner submits the group pre-order <request>
    Then the request is rejected with status 404 and message "<message>"
    And the group reservation has status "Open"

    Examples:
      | what            | request                             | message                         |
      | the group       | of a group that does not exist      | Group reservation was not found |
      | the reservation | for a reservation that does not exist | Reservation not found.        |

  Scenario: Submit is rejected when the group belongs to another reservation
    Given the owner has booked another table for 3 guests
    And these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
    When the owner submits the group pre-order for the other reservation
    Then the request is rejected with status 400 and message "Group reservation does not belong to this reservation."

  Scenario: Submitting the group a second time
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 2 x Garden Soup | submitted |
    And the owner has submitted the group pre-order
    When the owner submits the group pre-order
    Then the request is rejected with status 400 and message "Group reservation was submitted."
    And the error code is "GroupReservationSubmitted"
    And the restaurant order has status "SubmittedByCustomer" with dishes:
      | dish        | quantity |
      | Berry Cake  | 1        |
      | Garden Soup | 2        |

  Scenario Outline: Only the owner can submit the group pre-order (<who>)
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
    When <who> submits the owner's group pre-order
    Then the request is rejected with status <status> and message "<message>"
    And the group reservation has status "Open"

    Examples:
      | who                  | status | message                                                    |
      | another customer     | 403    | Only the reservation owner can submit the group pre-order. |
      | the waiter           | 403    | Only the reservation owner can submit the group pre-order. |
      | guest Anna           | 401    | Unauthorized                                               |
      | an anonymous visitor | 401    | Unauthorized                                               |

  Scenario: Guests cannot change their carts after the group is submitted
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
      | Boris | 1 x Green Salad | open      |
    And the owner has submitted the group pre-order
    When guest Boris adds "1 x Berry Cake" to the cart
    Then the request is rejected with status 409 and message "Group reservation was submitted."
