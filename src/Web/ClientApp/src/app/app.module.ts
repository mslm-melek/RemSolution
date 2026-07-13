import { BrowserModule } from '@angular/platform-browser';
import { APP_ID, NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { HomeComponent } from './home/home.component';
import { AuthorizeInterceptor } from 'src/api-authorization/authorize.interceptor';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { BrandComponent } from './brand/brand.component';
import { ModelCarComponent } from './model-car/model-car.component';
import { ModelCarFormComponent } from './model-car/model-car-form.component';
import { CarComponent } from './car/car.component';
import { CarFormComponent } from './car/car-form.component';
import { ClientComponent } from './client/client.component';
import { ClientFormComponent } from './client/client-form.component';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';

@NgModule({
  declarations: [
    AppComponent,
    NavMenuComponent,
    HomeComponent,
    CarComponent,
    CarFormComponent,
    ModelCarComponent,
    ModelCarFormComponent,
    BrandComponent,
    ClientComponent,
    ClientFormComponent
  ],
  bootstrap: [AppComponent],
  imports: [
    BrowserModule,
    FormsModule,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatToolbarModule,
    MatSelectModule,
    MatTooltipModule,
    RouterModule.forRoot([
      { path: '', component: HomeComponent, pathMatch: 'full' },
      { path: 'brand', component: BrandComponent },
      { path: 'model-car', component: ModelCarComponent },
      { path: 'model-car/new', component: ModelCarFormComponent },
      { path: 'model-car/:id', component: ModelCarFormComponent },
      { path: 'car', component: CarComponent },
      { path: 'car/new', component: CarFormComponent },
      { path: 'car/:id', component: CarFormComponent },
      { path: 'client', component: ClientComponent },
      { path: 'client/new', component: ClientFormComponent },
      { path: 'client/:id', component: ClientFormComponent }
    ]),
    BrowserAnimationsModule],
  providers: [
    { provide: APP_ID, useValue: 'ng-cli-universal' },
    { provide: HTTP_INTERCEPTORS, useClass: AuthorizeInterceptor, multi: true },
    provideHttpClient(withInterceptorsFromDi()),
    provideAnimationsAsync()
  ]
})
export class AppModule { }
