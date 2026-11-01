@api
Feature: Feedback / Reviews

  After a visit, a customer leaves feedback about service or kitchen — with a rating and a comment.
  Feedback can be edited. Location feedback is filterable by type, sortable, and paginated.

  Background:
    Given a customer who is signed in
    And a table that seats 4 guests
    And the customer has booked the table for tomorrow evening
    And the customer has completed the visit (reservation status is "Completed")

  Rule: A customer leaves feedback for their completed visit

    @api @smoke
    Scenario: A customer creates a service feedback
      When the customer creates a feedback of type "Service" with rating 5 and comment "Excellent service!"
      Then the feedback is created
      And the feedback shows rating 5 and comment "Excellent service!"

    @api
    Scenario: A customer creates a kitchen feedback
      When the customer creates a feedback of type "Kitchen" with rating 4 and comment "Good food"
      Then the feedback is created
      And the feedback shows rating 4 and comment "Good food"

    @api
    Scenario: A customer cannot leave feedback for a reservation that is not completed
      Given the customer has a reservation with status "Reserved"
      When the customer tries to create a feedback
      Then the action is refused

    @api
    Scenario: A customer cannot leave feedback for another customer's reservation
      Given another customer who is signed in
      And that customer has a completed reservation
      When this customer tries to create feedback for that reservation
      Then the action is refused

    @api
    Scenario: A customer edits their feedback
      Given the customer has created a feedback
      When the customer edits the feedback to rating 3 and comment "It was okay"
      Then the feedback shows rating 3 and comment "It was okay"

    @api
    Scenario: Feedback rating must be between 1 and 5
      When the customer tries to create a feedback with rating 0
      Then the action is refused
      And the visitor is told "Rating must be between 1 and 5"

    @api
    Scenario: Feedback rating must be between 1 and 5 (upper bound)
      When the customer tries to create a feedback with rating 6
      Then the action is refused
      And the visitor is told "Rating must be between 1 and 5"

  Rule: Location feedback listing

    Background:
      Given the location has multiple feedback entries of different types

    @api
    Scenario Outline: Filtering feedback by type
      When a guest requests feedback of type <type>
      Then all feedback shown is of type <type>

      Examples:
        | type     |
        | Service  |
        | Kitchen  |

    @api
    Scenario Outline: Sorting feedback
      When a guest requests feedback sorted by <sort>
      Then the feedback is sorted <order>

      Examples:
        | sort                     | order                    |
        | highest rating first     | from highest to lowest   |
        | lowest rating first      | from lowest to highest   |
        | newest first             | from newest to oldest    |
        | oldest first             | from oldest to newest    |

    @api
    Scenario: Feedback pagination works
      When a guest requests page 1 of feedback with page size 2
      Then at most 2 feedback items are returned

    @api
    Scenario: Combined filter, sort, and pagination
      When a guest requests Kitchen feedback sorted by highest rating first, page 1
      Then the feedback is filtered, sorted, and paginated correctly