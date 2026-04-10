const cron = require("node-cron");
const axios = require("axios");

const BASE_URL = "http://localhost:9005";
const ENDPOINTS = {
  getData: `${BASE_URL}/getData`,
  email: `${BASE_URL}/email`,
  sms: `${BASE_URL}/sms`,
  discord: `${BASE_URL}/discord`,
};

let activeJobs = [];

const sendRequest = async (url, data) => {
  try 
  {
    const response = await axios.post(url, data);
    return response.data;
  } 
  catch (error)
  {
    console.error(`Error sending request to ${url}:`, error);
    return null;
  }
};

const scheduleJob = (cronExpression, userId, requestData, notifications) => {
  if (!cron.validate(cronExpression))
  {
    throw new Error("Invalid cron expression");
  }

  const job = cron.schedule(
    cronExpression,
    async () => {
      try {
        const fetchedData = await sendRequest(ENDPOINTS.getData, requestData);
        const notificationData = { data: fetchedData, user: userId };

        if (fetchedData ==null) return;

        const { otoMotoData,otoDomData, olxData, sprzedajemyData, message, success } = fetchedData;

        if ((!otoMotoData || otoMotoData.length === 0) && (!olxData || olxData.length === 0) && (!otoDomData || otoDomData.length === 0)&& (!sprzedajemyData || sprzedajemyData.length === 0)) {
          console.log("Mail nie wysłany - brak danych!!!!")
          return;
        }

        if (notifications.email) 
        {
          await sendRequest(ENDPOINTS.email, {
            dataToSend: fetchedData,
            receiverId: userId,
          });
        }
        if (notifications.sms) 
        {
          await sendRequest(ENDPOINTS.sms, notificationData);
        }
        if (notifications.discord) 
        {
          await sendRequest(ENDPOINTS.discord, notificationData);
        }
      } 
      catch (error) 
      {
        console.error("Error executing scheduled job:", error);
      }
    },
    { scheduled: true, timezone: "Europe/Warsaw" }
  );

  activeJobs.push({ job, userId });
  console.log(`Sending job successfully registered for userId: ${userId}`);
};

const sendNotificationJob = async (req, res) => {
  try 
  {
    const { godzina_maila, repeat_after_specified_time, phrase, request_number, user_id, additional_phrase, ...services } = req.body;
    if (!user_id) throw new Error("User ID is required");

    const allowedWebsites = ["olx", "amazon", "allegro", "pepper", "otoMoto", "otoDom", "sprzedajemy"];
    const websites = Object.keys(req.body).filter((key) => allowedWebsites.includes(key) && req.body[key]);
    
    const notifications = 
    {
      email: Boolean(services.email),
      sms: Boolean(services.sms),
      discord: Boolean(services.discord),
    };

    const requestData = { websites, phrase, request_number, additional_phrase };
    
    if (godzina_maila) 
    {
      const [hour, minute] = godzina_maila.split(":"), cronExpression = `${minute} ${hour} * * *`;
      scheduleJob(cronExpression, user_id, requestData, notifications);
    } 
    else if (repeat_after_specified_time) 
    {
      const cronExpression = `*/${repeat_after_specified_time} * * * *`;
      scheduleJob(cronExpression, user_id, requestData, notifications);
    } 
    else 
    {
      console.warn("No job scheduled");
    }

    res.status(201).json({ status: "OK", message: "Job successfully registered" });
  } 
  catch (error) 
  {
    console.error(error);
    res.status(500).json({ message: error.message || "An error occurred" });
  }
};

const deleteJobsForUser = async (req, res) => {
  try 
  {
    const { user_id } = req.body;
    if (!user_id) throw new Error("User ID is required");

    const removedJobs = activeJobs.filter((job) => {
      if (job.userId === user_id) 
      {
        job.job.stop();
        console.warn(`Job stopped for user ${user_id}`);
        return false;
      }
      return true;
    });

    const deletedCount = activeJobs.length - removedJobs.length;
    activeJobs = removedJobs;

    res.status(200).json({ status: "OK", success: true, message: `Deleted ${deletedCount} jobs for user ${user_id}` });
  } 
  catch (error) 
  {
    console.error(error);
    res.status(500).json({ message: error.message || "An error occurred while deleting jobs" });
  }
};

module.exports = { sendNotificationJob, deleteJobsForUser };