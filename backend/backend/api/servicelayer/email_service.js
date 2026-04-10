const emailDal=require('../dataaccesslayer/emailsDal');

const sendMailService = async (req) => {
  try {
      const result = await emailDal.sendMail(req);
      return result;
  } catch (error) {
      console.error('Error in sendMailService:', error.message);
      res.status(500).json({ success: false, message: 'Email sending failed.', error: error.message });
  }
};

module.exports={ sendMailService };