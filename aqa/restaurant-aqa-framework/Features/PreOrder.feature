@api
Feature: Pre-order

  A customer creates a pre-order for their reservation, modifies it, submits it,
  and can recall or cancel it. The waiter sees the pre-order and can confirm/cancel
  individual dishes.

  Background:
    Given a customer who is signed in
    And a table that seats 4 guests
    And the customer has booked the table for tomorrow evening
    And the restaurant has dishes on the menu

  Rule: A customer manages their pre-order

    @api @smoke
    Scenario: A customer creates a pre-order with multiple dishes
      When the customer adds a dish to their pre-order
      And the customer adds another dish to their pre-order
      Then the pre-order contains both dishes
      And the pre-order status is "Draft"

    @api
    Scenario: A customer modifies their pre-order
      Given the customer has a pre-order with a dish
      When the customer changes the quantity of that dish
      Then the pre-order shows the updated quantity

    @api
    Scenario: A customer removes a dish from their pre-order
      Given the customer has a pre-order with a dish
      When the customer removes that dish
      Then the pre-order no longer contains that dish

    @api
    Scenario: A customer submits their pre-order
      Given the customer has a pre-order with a dish
      When the customer submits the pre-order
      Then the pre-order status is "SubmittedByCustomer"
      And the reservation status is "Submitted"

    @api
    Scenario: A customer recalls their submitted pre-order
      Given the customer has submitted a pre-order
      When the customer recalls the pre-order
      Then the pre-order status is "Draft"
      And the reservation status is "Reserved"

    @api
    Scenario: A customer cancels their pre-order
      Given the customer has a pre-order with a dish
      When the customer cancels the pre-order
      Then the pre-order is deleted

    @api
    Scenario: A customer cannot submit an empty pre-order
      When the customer tries to submit an empty pre-order
      Then the submission is refused

  Rule: Waiter confirms and cancels pre-order dishes

    Background:
      Given a waiter who is signed in
      And the customer has submitted a pre-order with multiple dishes
      And the waiter is assigned to the reservation's table

    @api
    Scenario: The assigned waiter confirms a dish
      When the waiter confirms a dish in the pre-order
      Then the dish status is "Confirmed"

    @api
    Scenario: The assigned waiter cancels a dish
      When the waiter cancels a dish in the pre-order
      Then the dish status is "Cancelled"

    @api
    Scenario: Another waiter cannot confirm a dish
      Given another waiter who is signed in
      When that waiter tries to confirm a dish
      Then the action is refused

    @api
    Scenario: A customer cannot confirm a dish
      When the customer tries to confirm a dish
      Then the action is refused

  Rule: Waiter manages reservation status

    Background:
      Given a waiter who is signed in
      And a customer has a reservation with status "Reserved"
      And the waiter is assigned to the reservation's table

    @api
    Scenario Outline: The assigned waiter can change the reservation status
      When the waiter changes the reservation status to <status>
      Then the reservation status is <status>

      Examples: Valid status transitions
        | status          |
        | InService       |
        | Completed       |
        | Cancelled       |

    @api
    Scenario: The assigned waiter can reassign the reservation to another table
      Given another available table
      When the waiter reassigns the reservation to the other table
      Then the reservation is on the new table

    @api
    Scenario: Another waiter cannot change the reservation status
      Given another waiter who is signed in
      When that waiter tries to change the reservation status
      Then the action is refused

    @api
    Scenario: A customer cannot change the reservation status
      When the customer tries to change the reservation status
      Then the action is refused
