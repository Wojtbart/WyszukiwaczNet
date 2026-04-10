var express = require('express');
var router = express.Router();

const history = require('../controllers/historyController');

router.get('/history/:login', history.getHistory);

module.exports = router;