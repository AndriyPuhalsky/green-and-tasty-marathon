@L1 @api
Feature: L1 API. Owner views the participants of the group pre-order
  The owner invites guests by a link, every guest builds their own cart,
  and the owner sees who joined, what they ordered and how much it costs.

  Background:
    Given the owner has booked a table for 5 guests
    And the owner has shared the group invite

  @smoke
  Scenario: Owner sees every participant with dishes, quantities and totals
    Given these guests have joined the group by the invite:
      | guest | dishes                           | cart      |
      | Anna  | 2 x Garden Soup, 1 x Green Salad | submitted |
      | Boris | 1 x Veggie Burger                | open      |
    When the owner requests the group participants
    Then the request succeeds
    And the participants are:
      | name  | type  | dishes                           | total | submitted |
      | Anna  | Guest | 2 x Garden Soup, 1 x Green Salad | 23.25 | yes       |
      | Boris | Guest | 1 x Veggie Burger                | 12.00 | no        |
    And the group total is 35.25 and equals the sum of the participant totals

  Scenario: Group total follows the changes in a guest cart
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart |
      | Anna  | 1 x Garden Soup | open |
      | Boris | 1 x Berry Cake  | open |
    When guest Anna adds "2 x Green Salad" to the cart
    Then the owner sees that Anna has a total of 21.00
    And the group total is 26.75 and equals the sum of the participant totals
    When guest Anna removes "1 x Garden Soup" from the cart
    Then the owner sees that Anna has a total of 12.50
    And the group total is 18.25 and equals the sum of the participant totals

  Scenario: Participants who have not submitted a cart or have no dishes are listed too
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
      | Boris | 1 x Berry Cake  | open      |
      | Clara |                 | open      |
    When the owner requests the group participants
    Then the participants are:
      | name  | type  | dishes          | total | submitted |
      | Anna  | Guest | 1 x Garden Soup | 8.50  | yes       |
      | Boris | Guest | 1 x Berry Cake  | 5.75  | no        |
      | Clara | Guest |                 | 0     | no        |

  Scenario: Nobody has joined the group yet
    When the owner requests the group participants
    Then the request succeeds
    And the group has no participants and the total is 0

  Scenario Outline: Only the owner of the group can see the participants (<who>)
    Given guest Anna has joined the group by the invite
    When <who> requests the participants of the owner's group
    Then the request is rejected with status <status> and message "<message>"

    Examples:
      | who                  | status | message                                            |
      | another customer     | 403    | Only the reservation owner can perform this action. |
      | the waiter           | 403    | Only the reservation owner can perform this action. |
      | guest Anna           | 401    | Unauthorized                                       |
      | an anonymous visitor | 401    | Unauthorized                                       |

  Scenario: Group that does not exist
    When the owner requests the participants of a group that does not exist
    Then the request is rejected with status 404 and message "Group reservation was not found"
