require('iconv-lite').encodingExists('cesu8');
const requests = require('supertest');
// jest.useRealTimers();
// jest.useFakeTimers('legacy');
const cron = require('./api/controllers/cron');
const usersDal = require('./api/dataaccesslayer/usersDal');
const users = require('./api/models/Users_model');

let app=null;
beforeAll(async () => {
    console.log("Początek testow")
    app=await require('./app');
    
});
afterAll(async () => {
    console.log("Koniec testow")
});

// TESTY
describe("Testy dla użytkowników", () => {

    it('Pobranie użytkownika test', async() => {
        const response = await requests(app).get("/getUser/test");
        expect(response.body.user_id).toEqual(9);
        expect(response.statusCode).toBe(201);
    });

    it('Pobranie konfiguracji użytkownika test', async() => {
        const response = await requests(app).get("/getConfiguration/test");
        expect(response.body.data).toEqual({"aktualna_oferta": true, "allegro": true, "amazon": true, "discord": false, "email": false, "godzina_maila": null, "id": 1, "olx": false, "pepper": true, "repeat_after_specified_time": 0, "sms": false, "user_id": 9});
        expect(response.statusCode).toBe(201);
    });
});

// describe('Test dla funkcji deleteJobsForUser', () => {
//     let mockRequest;
//     let mockResponse;
  
//     beforeEach(() => {
//       mockRequest = (user_id) => ({ body: { user_id } });
//       mockResponse = () => {
//         const res = {};
//         res.status = jest.fn().mockReturnValue(res);
//         res.json = jest.fn().mockReturnValue(res);
//         res.send = jest.fn().mockReturnValue(res);
//         return res;
//       };
//     });
  
//     it('Wywołanie funkcji usuwania jobów dla konkretnego użytkownika', async () => {
//       const user_id = mockRequest(9); //użytkownik 9
//       const res = mockResponse();
  
//       await cron.deleteJobsForUser(user_id, res);
//       expect(res.status).toHaveBeenCalledWith(201);
//       expect(res.json).toHaveBeenCalledWith({
//         status: 'OK',
//         message: 'Ilość usuniętych zadań dla użytkownika 9 to: 0',
//       }); 
//     });
//   });

//   describe('registerUser', () => {
//     let mockRequest;
//     let mockResponse;
  
//     beforeEach(() => {
//       mockRequest = (userData) => ({ body: userData });
//       mockResponse = () => {
//         const res = {};
//         res.status = jest.fn().mockReturnValue(res);
//         res.json = jest.fn().mockReturnValue(res);
//         res.send = jest.fn().mockReturnValue(res);
//         return res;
//       };
//     });
  
//     it('should register a new user and return the user object', async () => {
//       const req = mockRequest({
//         name: 'John',
//         surname: 'Doe',
//         email: 'john.doe@example.com',
//         password: 'password123',
//         phone: '123456789',
//         login: 'johndoe',
//       });
//       const res = mockResponse();
  
//       const mockUser = {
//         id: '52',
//         name: 'John',
//         surname: 'Doe',
//         email: 'john.doe@example.com',
//         password: 'password123',
//         phone: '123456789',
//         login: 'johndoe',
//       };
      
//       users.Users_models.create= jest.fn().mockResolvedValue(mockUser); // Mockowanie metody create z modelu Users_models
  
//       await usersDal.registerUser(req, res);
 

//     //   expect(res.json).toHaveBeenCalledWith({
//     //     status: 'OK',
//     //     message: "Poprawnie zarejestrowano użytkownika!",
//     //   });

//       expect(users.Users_models.create).toHaveBeenCalledWith({
//         name: 'John',
//         surname: 'Doe',
//         email: 'john.doe@example.com',
//         password: 'password123',
//         phone: '123456789',
//         login: 'johndoe',
//       }); // Sprawdzenie, czy metoda create została wywołana z oczekiwanymi danymi
  
//       expect(res.json).toHaveBeenCalledWith(mockUser); // Sprawdzenie, czy odpowiedź JSON została wysłana z oczekiwanym obiektem użytkownika
//     });
  
    // it('should handle errors and send a 500 response', async () => {
    //   const req = mockRequest({
    //     name: 'John',
    //     surname: 'Doe',
    //     email: 'john.doe@example.com',
    //     password: 'password123',
    //     phone: '123456789',
    //     login: 'johndoe',
    //   });
    //   const res = mockResponse();
  
    //   const errorMessage = 'An error occurred while registering the user!';
    //   const error = new Error(errorMessage);
    //   users.Users_models.create = jest.fn().mockRejectedValue(error); // Mockowanie metody create, aby zwróciła odrzucony Promise z błędem
  
    //   await usersDal.registerUser(req, res);
  
    //   expect(res.status).toHaveBeenCalledWith(500); // Sprawdzenie, czy status został ustawiony na 500
    //   expect(res.send).toHaveBeenCalledWith({
    //     message: errorMessage,
    //   }); // Sprawdzenie, czy odpowiedź z błędem została wysłana
    // });
//   });
  