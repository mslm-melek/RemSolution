import { Component } from '@angular/core';
import { CarsClient, CarDto } from '../web-api-client';

@Component({
  selector: 'app-car',
  templateUrl: './car.component.html'
})
export class CarComponent {

  constructor(private client: CarsClient) {


  }
}
