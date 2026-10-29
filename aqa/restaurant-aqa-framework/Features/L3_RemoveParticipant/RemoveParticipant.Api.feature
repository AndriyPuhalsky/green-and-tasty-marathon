@L3 @api
Feature: L3 API. Owner removes a participant from the group
  After removal the response contains the updated participants and the new total.
  Only a participant of this group can be removed, and only by the owner.

  Background:
    Given the owner has booked a table for 5 guests
    And the owner has shared the group invite

  @smoke
  Scenario: Removing one participant of several
    Given these guests have joined the group by the invite:
      | guest | dishes                           | cart      |
      | Anna  | 2 x Garden Soup                  | submitted |
      | Boris | 1 x Veggie Burger, 1 x Berry Cake | open      |
      | Clara | 1 x Green Salad                  | submitted |
    When the owner removes guest Boris from the group
    Then the request succeeds
    And the response lists the participants "Anna, Clara" with a group total of 23.25
    And the participants are:
      | name  | type  | dishes          | total | submitted |
      | Anna  | Guest | 2 x Garden Soup | 17.00 | yes       |
      | Clara | Guest | 1 x Green Salad | 6.25  | yes       |

  Scenario: Removing a participant who has already submitted the cart
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 2 x Garden Soup | submitted |
      | Boris | 1 x Green Salad | submitted |
    When the owner removes guest Anna from the group
    Then the request succeeds
    And the response lists the participants "Boris" with a group total of 6.25
    When the owner submits the group pre-order
    Then the submitted dishes are:
      | dish        | quantity |
      | Green Salad | 1        |

  Scenario: Removing the last participant
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
    When the owner removes guest Anna from the group
    Then the request succeeds
    And the response has no participants and a group total of 0

  Scenario: The owner's own session cannot be removed
    Given the owner has opened their own session in the group
    When the owner removes their own session from the group
    Then the request is rejected with status 400 and message "Owner session cannot be removed from the group."

  Scenario: Participant of another group cannot be removed
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart |
      | Anna  | 1 x Garden Soup | open |
    And another owner has booked a table for 3 guests and shared the invite
    And these guests have joined the other owner's group by the invite:
      | guest | dishes         | cart |
      | Boris | 1 x Berry Cake | open |
    When the owner removes guest Boris of the other owner's group from their own group
    Then the request is rejected with status 400 and message "Guest session does not belong to this group reservation."
    And the participants of the other owner's group are "Boris"
    And guest Anna is still in the group

  Scenario: Participant that does not exist
    When the owner removes a participant that does not exist
    Then the request is rejected with status 404 and message "Guest session with id {missing id} not found."

  Scenario Outline: Only the owner can remove a participant (<who>)
    Given these guests have joined the group by the invite:
      | guest | dishes          | cart |
      | Anna  | 1 x Garden Soup | open |
      | Boris | 1 x Berry Cake  | open |
    When <who> removes guest Anna from the owner's group
    Then the request is rejected with status <status> and message "<message>"
    And guest Anna is still in the group

    Examples:
      | who                  | status | message                                             |
      | another customer     | 403    | Only the reservation owner can perform this action. |
      | the waiter           | 403    | Only the reservation owner can perform this action. |
      | guest Boris          | 401    | Unauthorized                                        |
      | guest Anna           | 401    | Unauthorized                                        |
      | an anonymous visitor | 401    | Unauthorized                                        |

  Scenario: Participants cannot be removed after the group is submitted
    Given the owner has a personal pre-order with "1 x Berry Cake"
    And these guests have joined the group by the invite:
      | guest | dishes          | cart      |
      | Anna  | 1 x Garden Soup | submitted |
    And the owner has submitted the group pre-order
    When the owner removes guest Anna from the group
    Then the request is rejected with status 400 and message "Group reservation was submitted."
