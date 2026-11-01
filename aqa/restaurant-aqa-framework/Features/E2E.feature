@ui @e2e
Feature: End-to-end scenario

  A complete happy path: book a table → create pre-order → waiter confirms dishes →
  waiter manages reservation → customer leaves feedback → feedback visible in location list.

  Background:
    Given a customer who is signed in
    And a table that seats 4 guests
    And the restaurant has dishes on the menu
    And a waiter who is signed in
    And the waiter is assigned to the reservation's table

  Rule: Complete customer journey

    @smoke
    Scenario: Customer books, pre-orders, waiter manages, customer reviews
      When the customer books the table for 2 guests
      And the customer adds a dish to their pre-order
      And the customer adds another dish to their pre-order
      And the customer submits the pre-order
      And the waiter confirms a dish in the pre-order
      And the waiter changes the reservation status to "InService"
      And the waiter changes the reservation status to "Completed"
      And the customer creates a feedback of type "Service" with rating 5 and comment "Excellent service!"
      Then the feedback is created
      And the feedback shows rating 5 and comment "Excellent service!"

  Rule: Role × Action × State matrix (automated as combinatorial table)

    @data-table
    Scenario Outline: Role-Action-State combinations
      Given a reservation with status <state>
      And an actor with role <role>
      When the <role> tries to <action>
      Then the result is <result>

      Examples: Reservation management
        | role        | action                              | state       | result    |
        | Customer    | view own reservation                | Reserved    | allowed   |
        | Customer    | view own reservation                | InService   | allowed   |
        | Customer    | view own reservation                | Completed   | allowed   |
        | Customer    | view own reservation                | Cancelled   | allowed   |
        | Customer    | modify reservation                  | Reserved    | allowed   |
        | Customer    | modify reservation                  | InService   | refused   |
        | Customer    | modify reservation                  | Completed   | refused   |
        | Customer    | modify reservation                  | Cancelled   | refused   |
        | Customer    | cancel reservation                  | Reserved    | allowed   |
        | Customer    | cancel reservation                  | InService   | refused   |
        | Customer    | cancel reservation                  | Completed   | refused   |
        | Customer    | cancel reservation                  | Cancelled   | refused   |
        | Waiter      | view assigned reservation           | Reserved    | allowed   |
        | Waiter      | view assigned reservation           | InService   | allowed   |
        | Waiter      | view assigned reservation           | Completed   | allowed   |
        | Waiter      | view assigned reservation           | Cancelled   | allowed   |
        | Waiter      | confirm pre-order dish              | Reserved    | refused   |
        | Waiter      | confirm pre-order dish              | Submitted   | allowed   |
        | Waiter      | confirm pre-order dish              | InService   | allowed   |
        | Waiter      | confirm pre-order dish              | Completed   | refused   |
        | Waiter      | change reservation status           | Reserved    | allowed   |
        | Waiter      | change reservation status           | InService   | allowed   |
        | Waiter      | change reservation status           | Completed   | allowed   |
        | Waiter      | change reservation status           | Cancelled   | refused   |
        | Waiter      | reassign table                      | Reserved    | allowed   |
        | Waiter      | reassign table                      | InService   | refused   |
        | OtherWaiter | view unassigned reservation         | Reserved    | refused   |
        | OtherWaiter | confirm pre-order dish              | Submitted   | refused   |
        | OtherWaiter | change reservation status           | Reserved    | refused   |