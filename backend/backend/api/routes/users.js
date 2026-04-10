var express = require('express');
var router = express.Router();

const users = require('../controllers/usersController');

// User registration and login
router.post('/registerUser', users.registerUser);
router.post('/login', users.login);

// User data retrieval
router.get('/getUser/:login', users.getUser);

// Configuration management
router.post('/saveConfiguration', users.saveConfiguration);
router.get('/getConfiguration/:login', users.getConfiguration);

module.exports = router;