import { Component } from '@angular/core';
import { ModelCarsClient, ModelCarDto } from '../web-api-client';

@Component({
  selector: 'app-model-car',
  templateUrl: './model-car.component.html'
})
export class ModelCarComponent {
  public brands: ModelCarDto[] = [];

  constructor(private client: ModelCarsClient) {
 
  }
}
