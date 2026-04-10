const jwt = require('jsonwebtoken');

const generateToken = (payload) => {
  const secretKey = 'yourSecretKey'; 
  const options = {
    expiresIn: '24h', 
  };

  const token = jwt.sign(payload, secretKey, options);
  return token;
};

module.exports = {
  generateToken
};