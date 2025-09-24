Feature: Cars Management
  In order to manage cars
  As an authenticated user
  I want to add and view cars

  @Cars
  Scenario: Add a new car
    Given I am on the Cars page
    When I create a car with matricule "123-ABC" and color "Red"
    Then I should see the car "123-ABC" in the list
