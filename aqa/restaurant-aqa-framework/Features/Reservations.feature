Feature: Reservations

  Booking a table. The rules below come from the product's business rules.

  Background:
    Given a customer who is signed in
    And a table that seats 4 guests

  Rule: A booking must make sense

    @api
    Scenario: A customer cannot book for more guests than the table seats
      When the customer books the table for 5 guests
      Then the customer is told the table is too small

    @api
    Scenario Outline: A booking must start far enough ahead
      When the customer books the table to start in <minutes> minutes
      Then the booking is <outcome>

      Examples: Around the 15 minute notice period
        | minutes | outcome  |
        | 5       | refused  |
        | 14      | refused  |
        | 16      | accepted |

    @api
    Scenario: Two bookings cannot overlap on the same table
      Given the customer has booked the table for tomorrow evening
      When another customer books the same table for the same time
      Then the second booking is refused

  Rule: A customer manages their own bookings

    @api
    Scenario: A customer sees their booking in their list
      Given the customer has booked the table for tomorrow evening
      Then the reservation appears in my reservations with correct time, table and guest count

    @api
    Scenario: A booked table is no longer offered for that time
      Given the customer has booked the table for tomorrow evening
      Then this table is no longer offered for that time

    @api
    Scenario: A customer can cancel their booking
      Given the customer has booked the table for tomorrow evening
      When the customer cancels the reservation
      Then the cancellation succeeds

    @api
    Scenario: A customer cannot cancel the same booking twice
      Given the customer has booked the table for tomorrow evening
      When the customer cancels the reservation
      And the customer cancels the reservation again
      Then the second cancellation is refused

    @api
    Scenario Outline: A customer can change their booking within the rules
      Given the customer has booked the table for tomorrow evening
      When the customer changes the reservation to start in <minutes> minutes
      Then the reservation is updated

      Examples: Valid new start times
        | minutes |
        | 16      |
        | 30      |
        | 60      |

    @api
    Scenario Outline: A customer cannot change their booking to an invalid time
      Given the customer has booked the table for tomorrow evening
      When the customer changes the reservation to start in <minutes> minutes
      Then the booking is refused

      Examples: Invalid new start times
        | minutes |
        | 5       |
        | 14      |

  Rule: Authorization is enforced

    @api
    Scenario: An unauthorized user cannot book a table
      When an unauthorized user tries to book the table
      Then the unauthorized request is refused

    @api
    Scenario: A customer cannot view another customer's reservation
      Given the customer has booked the table for tomorrow evening
      When another customer tries to view this reservation
      Then the other customer cannot see the reservation

  Rule: Boundary values for guest count

    @api
    Scenario Outline: Booking at and around table capacity
      When the customer books the table for <guestCount> guests
      Then the booking is <outcome>

      Examples: Capacity boundaries
        | guestCount | outcome  |
        | 3          | accepted |
        | 4          | accepted |
        | 5          | refused  |