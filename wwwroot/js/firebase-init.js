// Firebase App initialization and Firestore helper functions.
// Replace the configuration values with your Firebase project's settings.

const firebaseConfig = {
  apiKey: "YOUR_API_KEY",
  authDomain: "YOUR_PROJECT_ID.firebaseapp.com",
  projectId: "YOUR_PROJECT_ID",
  storageBucket: "YOUR_PROJECT_ID.appspot.com",
  messagingSenderId: "YOUR_MESSAGING_SENDER_ID",
  appId: "YOUR_APP_ID"
};

import { initializeApp } from 'https://www.gstatic.com/firebasejs/12.14.0/firebase-app.js';
import { getFirestore, collection, getDocs } from 'https://www.gstatic.com/firebasejs/12.14.0/firebase-firestore.js';

window.firebaseApp = initializeApp(firebaseConfig);
window.firebaseDb = getFirestore(window.firebaseApp);

window.loadDriversFromFirestore = async function () {
  const driversCollection = collection(window.firebaseDb, 'drivers');
  const snapshot = await getDocs(driversCollection);
  return snapshot.docs.map(doc => ({ id: doc.id, ...doc.data() }));
};

window.firebaseLoaded = true;
